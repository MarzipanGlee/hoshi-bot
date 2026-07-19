using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.MemberLore;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Turns finished member-lore interviews into structured notes. For each MemberLore-enabled guild it
// finds completed-but-not-yet-extracted interviews, runs the note extractor over each transcript, and
// writes the results: the interviewee's own bio fields auto-publish into their GuildMemberNote (empty
// fields only), while stories they told about *other* members land as Pending MemberNoteSuggestions
// for staff review. Runs off the DM hot path so the interview itself stays snappy, and is retryable
// (ExtractedAt is only set on success). See docs/ai-chat-member-lore.md.
//
// DisallowConcurrentExecution: the scheduler's immediate first run plus a scheduled tick could both
// pick up the same not-yet-extracted interview and double-write notes/suggestions.
[DisallowConcurrentExecution]
public class MemberInterviewExtractionJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    AiChatModelResolver modelResolver,
    MemberNoteService noteService,
    MemberNoteExtractor extractor,
    ILogger<MemberInterviewExtractionJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var guildIds = await db.GuildEnabledFeatures
            .Where(f => f.Feature == GuildFeature.MemberLore)
            .Select(f => f.GuildId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var guildId in guildIds)
        {
            try
            {
                await ProcessGuildAsync(guildId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Member-note extraction failed for guild {GuildId}", guildId);
            }
        }
    }

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var pending = await db.MemberInterviews
            .Where(i => i.GuildId == guildId && i.Status == MemberInterviewStatus.Completed && i.ExtractedAt == null)
            .OrderBy(i => i.CompletedAt)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
            return;

        var model = await modelResolver.ResolveAsync(guildId);
        if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
        {
            logger.LogInformation("MemberLore extraction for guild {GuildId} skipped: AI chat (Gemini) has no API key configured.", guildId);
            return;
        }

        var (idToName, nameToIds, personKeyById) = await BuildRosterAsync(guildId, cancellationToken);

        var extracted = 0;
        var suggestions = 0;
        foreach (var interview in pending)
        {
            var transcript = await db.MemberInterviewMessages
                .Where(m => m.InterviewId == interview.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new AiChatTurn(
                    m.Role == MemberInterviewRole.Bot ? AiChatRole.Assistant : AiChatRole.User, m.Content))
                .ToListAsync(cancellationToken);
            if (transcript.Count == 0)
            {
                interview.ExtractedAt = DateTimeOffset.UtcNow;
                continue;
            }

            var intervieweeName = idToName.GetValueOrDefault(interview.DiscordUserId, "Mitglied");
            var result = await extractor.ExtractAsync(model, intervieweeName, transcript, cancellationToken);
            if (result is null)
                continue; // leave ExtractedAt null so a transient failure retries next run

            await ApplySelfAsync(guildId, interview.DiscordUserId, result.Self, interview, cancellationToken);
            suggestions += AddPeerSuggestions(guildId, interview, result.Peers, nameToIds, personKeyById);

            interview.ExtractedAt = DateTimeOffset.UtcNow;
            extracted++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "MemberLore extraction for guild {Guild}: {Extracted}/{Pending} interview(s) extracted, {Suggestions} peer suggestion(s) queued.",
            guildId, extracted, pending.Count, suggestions);
    }

    // Fill the interviewee's own empty self-fields directly (auto-publish — consensual, about them);
    // when a self-field already holds member-curated text, queue the extracted value as a suggestion
    // instead of clobbering it.
    private async Task ApplySelfAsync(ulong guildId, ulong userId, MemberNoteExtractor.SelfInfo? self, MemberInterview interview, CancellationToken cancellationToken)
    {
        if (self is null)
            return;

        var note = await noteService.GetOrCreateAsync(guildId, userId, cancellationToken);
        var nicknames = self.Nicknames is { Count: > 0 } ? string.Join(", ", self.Nicknames.Where(n => !string.IsNullOrWhiteSpace(n))) : null;

        ApplySelfField(note, interview, MemberNoteField.PreferredName, self.PreferredName, userId);
        ApplySelfField(note, interview, MemberNoteField.Nicknames, nicknames, userId);
        ApplySelfField(note, interview, MemberNoteField.Interests, self.Interests, userId);
        ApplySelfField(note, interview, MemberNoteField.Background, self.Background, userId);
        ApplySelfField(note, interview, MemberNoteField.Languages, self.Languages, userId);
    }

    private void ApplySelfField(GuildMemberNote note, MemberInterview interview, MemberNoteField field, string? value, ulong userId)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
            return;

        var existing = MemberNoteService.GetField(note, field);
        if (string.IsNullOrWhiteSpace(existing))
        {
            MemberNoteService.ApplyField(note, field, value);
            return;
        }

        if (existing.Contains(value, StringComparison.OrdinalIgnoreCase))
            return; // already covered — nothing to add or review

        // Member already curated this field: don't overwrite; queue for review against themselves.
        db.MemberNoteSuggestions.Add(new MemberNoteSuggestion
        {
            GuildId = note.GuildId,
            TargetDiscordUserId = userId,
            TargetNameRaw = note.PreferredName ?? "",
            Field = field,
            SuggestedText = value,
            SourceInterviewId = interview.Id,
            SourceDiscordUserId = userId,
            Status = MemberNoteSuggestionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    private int AddPeerSuggestions(ulong guildId, MemberInterview interview, List<MemberNoteExtractor.PeerInfo> peers,
        IReadOnlyDictionary<string, List<ulong>> nameToIds, IReadOnlyDictionary<ulong, string> personKeyById)
    {
        var added = 0;
        foreach (var peer in peers)
        {
            var name = peer.Name?.Trim();
            var text = peer.Text?.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(text))
                continue;

            db.MemberNoteSuggestions.Add(new MemberNoteSuggestion
            {
                GuildId = guildId,
                TargetDiscordUserId = ResolveTarget(name, nameToIds, personKeyById),
                TargetNameRaw = name,
                Field = MapField(peer.Field),
                SuggestedText = text,
                SourceInterviewId = interview.Id,
                SourceDiscordUserId = interview.DiscordUserId,
                Status = MemberNoteSuggestionStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            added++;
        }
        return added;
    }

    private static MemberNoteField MapField(string? field) => field?.Trim().ToLowerInvariant() switch
    {
        "teaseabout" => MemberNoteField.TeaseAbout,
        "interests" => MemberNoteField.Interests,
        _ => MemberNoteField.RunningJokes,
    };

    // Resolve an extracted name to a target account. A unique match resolves. When a name maps to
    // several accounts: if they're all the same person (shared person key, e.g. player-linked alts)
    // resolve to one of them — injection consolidates that person's lore anyway; if they're different
    // people, return null so staff assign it on review rather than mis-attributing.
    private static ulong? ResolveTarget(string name, IReadOnlyDictionary<string, List<ulong>> nameToIds, IReadOnlyDictionary<ulong, string> personKeyById)
    {
        if (!nameToIds.TryGetValue(name.Trim().ToLowerInvariant(), out var ids) || ids.Count == 0)
            return null;
        if (ids.Count == 1)
            return ids[0];

        var distinctPeople = ids.Select(id => personKeyById.GetValueOrDefault(id, $"user:{id}")).Distinct().Count();
        return distinctPeople == 1 ? ids[0] : null;
    }

    private async Task<(Dictionary<ulong, string> IdToName, Dictionary<string, List<ulong>> NameToIds, Dictionary<ulong, string> PersonKeyById)> BuildRosterAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var idToName = new Dictionary<ulong, string>();
        var nameToIds = new Dictionary<string, List<ulong>>(StringComparer.OrdinalIgnoreCase);
        var rosterIds = new HashSet<ulong>();

        await foreach (var member in gatewayClient.Rest.GetGuildUsersAsync(guildId).WithCancellation(cancellationToken))
        {
            if (member.IsBot)
                continue;
            var name = CommanderName.Of(member);
            idToName[member.Id] = name;
            rosterIds.Add(member.Id);
            AddName(nameToIds, name, member.Id);
        }

        // Fold in known nicknames/preferred names so peer stories that use an alias still resolve.
        var notes = await db.GuildMemberNotes.Where(n => n.GuildId == guildId).ToListAsync(cancellationToken);
        foreach (var note in notes)
        {
            rosterIds.Add(note.DiscordUserId);
            foreach (var alias in Aliases(note))
                AddName(nameToIds, alias, note.DiscordUserId);
        }

        var personKeyById = await noteService.GetPersonKeysAsync(rosterIds, cancellationToken);
        return (idToName, nameToIds, personKeyById);
    }

    // Register a name/alias → account id, de-duplicated (a display name and an alias can coincide).
    private static void AddName(Dictionary<string, List<ulong>> nameToIds, string name, ulong id)
    {
        var key = name.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key))
            return;
        if (!nameToIds.TryGetValue(key, out var ids))
            nameToIds[key] = ids = [];
        if (!ids.Contains(id))
            ids.Add(id);
    }

    private static IEnumerable<string> Aliases(GuildMemberNote note)
    {
        if (!string.IsNullOrWhiteSpace(note.PreferredName))
            yield return note.PreferredName.Trim();
        if (!string.IsNullOrWhiteSpace(note.Nicknames))
            foreach (var nick in note.Nicknames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return nick;
    }
}
