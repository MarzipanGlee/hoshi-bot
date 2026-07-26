using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// DB operations over GuildMemberNote / MemberNoteSuggestion, shared by the extraction job
// (HoshiBot.Discord) and the Web editors. Kept NetCord-free so HoshiBot.Web can use it without a
// Discord project reference — target-name → user-id resolution (which needs the Discord roster) lives
// in the extraction job, not here. See docs/ai-chat-member-lore.md.
public class MemberNoteService(HoshiBotDbContext db)
{
    public Task<GuildMemberNote?> GetAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default) =>
        db.GuildMemberNotes.FirstOrDefaultAsync(n => n.GuildId == guildId && n.DiscordUserId == userId, cancellationToken);

    public Task<List<GuildMemberNote>> GetForGuildAsync(ulong guildId, CancellationToken cancellationToken = default) =>
        db.GuildMemberNotes.Where(n => n.GuildId == guildId).ToListAsync(cancellationToken);

    // Maps each Discord account to a stable "person key" so lore can be consolidated across a person's
    // multiple accounts: every account in one PlayerLinkService account group (accounts that share a
    // linked player, transitively) gets "player:{lowest player id in the group}"; an account with no
    // links is its own "user:{id}". Same-person alts thus collapse to one key; genuinely different
    // people never do. The key moves if the person later links a lower-id player — acceptable, and no
    // worse than the main-player key this replaced.
    public async Task<Dictionary<ulong, string>> GetPersonKeysAsync(IEnumerable<ulong> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        var groups = await PlayerLinkService.GetAccountGroupsAsync(db, ids, cancellationToken);

        var groupUserIds = groups.Values.SelectMany(g => g).Distinct().ToList();
        var links = await db.UserPlayers
            .Where(up => groupUserIds.Contains(up.DiscordUserId))
            .Select(up => new { up.DiscordUserId, up.StfcPlayerId })
            .ToListAsync(cancellationToken);
        var playersByUser = links
            .GroupBy(l => l.DiscordUserId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.StfcPlayerId).ToList());

        return ids.ToDictionary(
            id => id,
            id =>
            {
                var playerIds = groups[id].SelectMany(u => playersByUser.GetValueOrDefault(u) ?? []).ToList();
                return playerIds.Count > 0 ? $"player:{playerIds.Min()}" : $"user:{id}";
            });
    }

    public async Task<GuildMemberNote> GetOrCreateAsync(ulong guildId, ulong userId, CancellationToken cancellationToken = default)
    {
        var note = await db.GuildMemberNotes.FirstOrDefaultAsync(n => n.GuildId == guildId && n.DiscordUserId == userId, cancellationToken);
        if (note is null)
        {
            note = new GuildMemberNote { GuildId = guildId, DiscordUserId = userId };
            db.GuildMemberNotes.Add(note);
        }
        return note;
    }

    public Task<List<MemberNoteSuggestion>> GetPendingSuggestionsAsync(ulong guildId, CancellationToken cancellationToken = default) =>
        db.MemberNoteSuggestions
            .Where(s => s.GuildId == guildId && s.Status == MemberNoteSuggestionStatus.Pending)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<int> CountPendingSuggestionsAsync(ulong guildId, CancellationToken cancellationToken = default) =>
        db.MemberNoteSuggestions.CountAsync(s => s.GuildId == guildId && s.Status == MemberNoteSuggestionStatus.Pending, cancellationToken);

    // Approve a suggestion: merge its (possibly staff-edited) text into the resolved target member's
    // note field, then mark it Approved. Returns false if the suggestion is gone or has no target.
    public async Task<bool> ApproveSuggestionAsync(int suggestionId, ulong targetUserId, string text, CancellationToken cancellationToken = default)
    {
        var suggestion = await db.MemberNoteSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId, cancellationToken);
        if (suggestion is null)
            return false;

        var note = await GetOrCreateAsync(suggestion.GuildId, targetUserId, cancellationToken);
        ApplyField(note, suggestion.Field, MergeText(GetField(note, suggestion.Field), text));

        suggestion.Status = MemberNoteSuggestionStatus.Approved;
        suggestion.TargetDiscordUserId = targetUserId;
        suggestion.SuggestedText = text;
        suggestion.ReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RejectSuggestionAsync(int suggestionId, CancellationToken cancellationToken = default)
    {
        var suggestion = await db.MemberNoteSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId, cancellationToken);
        if (suggestion is null)
            return;
        suggestion.Status = MemberNoteSuggestionStatus.Rejected;
        suggestion.ReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPeerLoreHiddenAsync(ulong guildId, ulong userId, bool hidden, CancellationToken cancellationToken = default)
    {
        var note = await GetOrCreateAsync(guildId, userId, cancellationToken);
        note.PeerLoreHidden = hidden;
        await db.SaveChangesAsync(cancellationToken);
    }

    // Appends `addition` to `existing` on its own line, skipping exact duplicates. Used both when a
    // suggestion is approved and when extraction fills a field.
    public static string? MergeText(string? existing, string? addition)
    {
        addition = addition?.Trim();
        if (string.IsNullOrEmpty(addition))
            return existing;
        if (string.IsNullOrWhiteSpace(existing))
            return addition;
        if (existing.Contains(addition, StringComparison.OrdinalIgnoreCase))
            return existing;
        return existing.TrimEnd() + "\n" + addition;
    }

    public static bool IsPeerField(MemberNoteField field) =>
        field is MemberNoteField.RunningJokes or MemberNoteField.TeaseAbout;

    public static string? GetField(GuildMemberNote note, MemberNoteField field) => field switch
    {
        MemberNoteField.PreferredName => note.PreferredName,
        MemberNoteField.Nicknames => note.Nicknames,
        MemberNoteField.Interests => note.Interests,
        MemberNoteField.Background => note.Background,
        MemberNoteField.Languages => note.Languages,
        MemberNoteField.RunningJokes => note.RunningJokes,
        MemberNoteField.TeaseAbout => note.TeaseAbout,
        _ => null,
    };

    // Sets a field's value and stamps the matching author-side "updated" timestamp.
    public static void ApplyField(GuildMemberNote note, MemberNoteField field, string? value)
    {
        switch (field)
        {
            case MemberNoteField.PreferredName: note.PreferredName = value; break;
            case MemberNoteField.Nicknames: note.Nicknames = value; break;
            case MemberNoteField.Interests: note.Interests = value; break;
            case MemberNoteField.Background: note.Background = value; break;
            case MemberNoteField.Languages: note.Languages = value; break;
            case MemberNoteField.RunningJokes: note.RunningJokes = value; break;
            case MemberNoteField.TeaseAbout: note.TeaseAbout = value; break;
        }

        var now = DateTimeOffset.UtcNow;
        if (IsPeerField(field))
            note.PeerUpdatedAt = now;
        else
            note.SelfUpdatedAt = now;
    }
}
