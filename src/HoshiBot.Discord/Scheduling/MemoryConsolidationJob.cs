using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Forms Hoshi's episodic memory: for each AiChat guild with memory enabled, it distils the notable
// community events out of recent chat in her listen channels (the conversations she's part of) into
// GuildMemory rows — deduped and embedded so she can recall them later, anywhere. Runs off the answer
// path on a schedule, using the cheap member-lore model. Decay/prune runs here too. See the memory plan.
//
// DisallowConcurrentExecution: the immediate first run plus a scheduled tick could otherwise distil
// and store the same slice of chat twice before the watermark advances.
[DisallowConcurrentExecution]
public class MemoryConsolidationJob(
    HoshiBotDbContext db,
    GuildFeatureChannelService channelService,
    GuildFeatureSettingsService settingsService,
    AiChatIndexService indexService,
    AiChatModelResolver modelResolver,
    MemoryExtractor extractor,
    AiChatEmbeddingService embeddingService,
    MemoryService memoryService,
    MemberNoteService noteService,
    GuildFeatureService featureService,
    ILogger<MemoryConsolidationJob> logger) : IJob
{
    private const GuildAudience SettingsScope = GuildAudience.None;
    private const int MaxMessagesPerChannel = 60;
    private const int MaxCandidateChars = 8000;

    // Conversation memory (Phase 2): only summarise a channel whose new segment is substantial, keep a
    // few recent snapshots per channel, and store them at a modest fixed salience.
    private const int MinMessagesToSummarize = 6;
    private const int KeepSnapshotsPerChannel = 5;
    private const int ConversationSalience = 2;

    // Per-member interaction memory (Phase 3): a much lower bar than a whole-channel conversation —
    // one person's own volume in a window is naturally small, but a handful of their own messages is
    // still worth a personal recollection. Keeps the same rolling-per-person cap as the interview job
    // (one shared timeline regardless of source) and the same modest salience as conversation snapshots.
    private const int MinMessagesForMemberMemory = 3;
    private const int KeepMemoriesPerPerson = 5;
    private const int MemberMemorySalience = 2;

    public Task Execute(IJobExecutionContext context) =>
        // recheckAudience null: no per-guild audience re-check here — the body gates itself on the
        // MemoryEnabled setting (any audience) instead.
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.AiChat, null, logger,
            guildId => ProcessGuildAsync(guildId, context.CancellationToken), context.CancellationToken);

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        // MemoryEnabled is a per-audience AiChat setting, but the GuildMemory store and this job are
        // guild-wide — so run for the guild if memory is enabled under ANY audience (any true row).
        var memoryEnabled = await db.GuildFeatureSettingTexts.AnyAsync(
            s => s.GuildId == guildId && s.Feature == GuildFeature.AiChat
                && s.Key == AiChatSettingKeys.MemoryEnabled && s.Value.ToLower() == "true",
            cancellationToken);
        if (!memoryEnabled)
            return;

        // Recall + dedup need embeddings; without them a memory can't be found again, so don't form any.
        if (!await embeddingService.IsEnabledAsync(guildId))
        {
            logger.LogInformation("Memory consolidation for guild {GuildId} skipped: embeddings disabled.", guildId);
            return;
        }
        var embeddingModel = await embeddingService.GetModelAsync(guildId);

        var model = await modelResolver.ResolveLightweightAsync(guildId);
        if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
            return;

        // Watermark: only distil messages newer than the last run; first run looks back a day to bootstrap.
        var watermarkText = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryWatermark);
        var watermark = DateTimeOffset.TryParse(watermarkText, out var parsed) ? parsed : DateTimeOffset.UtcNow.AddDays(-1);

        var channels = await channelService.GetEnabledAudienceChannelsAsync(guildId, GuildFeature.AiChat);
        if (channels.Count == 0)
            return;

        // Gather new messages since the watermark: pooled (for guild-wide episodic distillation), grouped
        // per channel (for the per-channel conversation snapshots), and grouped per author (for
        // per-member interaction memories — across all channels, not just one, since the point is "what
        // has this person been up to", not "what happened in this channel").
        var episodicLines = new List<string>();
        var perChannelLines = new Dictionary<ulong, List<string>>();
        var perAuthorLines = new Dictionary<ulong, List<string>>();
        var authorNames = new Dictionary<ulong, string>();
        var newest = watermark;
        foreach (var channelId in channels)
        {
            var recent = await indexService.FetchRecentAsync(channelId, MaxMessagesPerChannel, cancellationToken);
            foreach (var message in recent.Where(m => m.CreatedAt > watermark).OrderBy(m => m.CreatedAt))
            {
                if (message.Author.IsBot)
                    continue;
                var text = AiChatIndexService.RenderMessageText(message);
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                var authorName = CommanderName.Of(message.Author);
                var line = $"{authorName}: {text}";
                episodicLines.Add(line);
                if (!perChannelLines.TryGetValue(channelId, out var channelLines))
                    perChannelLines[channelId] = channelLines = [];
                channelLines.Add(line);
                if (!perAuthorLines.TryGetValue(message.Author.Id, out var authorLines))
                    perAuthorLines[message.Author.Id] = authorLines = [];
                authorLines.Add(text);
                authorNames[message.Author.Id] = authorName;
                if (message.CreatedAt > newest)
                    newest = message.CreatedAt;
            }
        }

        if (episodicLines.Count == 0)
        {
            await SetWatermarkAsync(guildId, DateTimeOffset.UtcNow);
            return;
        }

        // Episodic: notable community events, distilled from all channels pooled together.
        var episodicStored = 0;
        foreach (var item in await extractor.ExtractAsync(model, Cap(string.Join("\n", episodicLines), MaxCandidateChars), cancellationToken))
        {
            var embedding = await embeddingService.EmbedAsync(guildId, item.Content!, cancellationToken);
            await memoryService.AddIfNovelAsync(new GuildMemory
            {
                GuildId = guildId,
                Scope = MemoryScope.Episodic,
                Content = item.Content!,
                Salience = item.Salience,
                CreatedAt = DateTimeOffset.UtcNow,
                Embedding = embedding,
                EmbeddingModel = embedding is null ? null : embeddingModel,
            }, cancellationToken);
            episodicStored++;
        }

        // Conversation snapshots: a short per-channel summary so a thread survives past the live window.
        var snapshots = 0;
        foreach (var (channelId, channelLines) in perChannelLines)
        {
            if (channelLines.Count < MinMessagesToSummarize)
                continue;
            var summary = await extractor.SummarizeConversationAsync(model, Cap(string.Join("\n", channelLines), MaxCandidateChars), cancellationToken);
            if (summary is null)
                continue;
            var embedding = await embeddingService.EmbedAsync(guildId, summary, cancellationToken);
            await memoryService.AddConversationSnapshotAsync(new GuildMemory
            {
                GuildId = guildId,
                Scope = MemoryScope.Conversation,
                ChannelId = channelId,
                SourceChannelId = channelId,
                Content = summary,
                Salience = ConversationSalience,
                CreatedAt = DateTimeOffset.UtcNow,
                Embedding = embedding,
                EmbeddingModel = embedding is null ? null : embeddingModel,
            }, KeepSnapshotsPerChannel, cancellationToken);
            snapshots++;
        }

        // Per-member interaction memories: what has this person personally been up to, distinct from
        // the structured GuildMemberNote lore fields. Person-key resolution (via MemberNoteService)
        // consolidates a person's alt accounts onto one rolling timeline.
        var memberMemories = 0;
        if (perAuthorLines.Count > 0)
        {
            var personKeyByUser = await noteService.GetPersonKeysAsync(perAuthorLines.Keys, cancellationToken);
            foreach (var (authorId, authorLines) in perAuthorLines)
            {
                if (authorLines.Count < MinMessagesForMemberMemory)
                    continue;
                var summary = await extractor.SummarizeMemberActivityAsync(model, authorNames[authorId], Cap(string.Join("\n", authorLines), MaxCandidateChars), cancellationToken);
                if (summary is null)
                    continue;
                var embedding = await embeddingService.EmbedAsync(guildId, summary, cancellationToken);
                await memoryService.AddMemberMemoryAsync(new GuildMemory
                {
                    GuildId = guildId,
                    Scope = MemoryScope.Member,
                    SubjectDiscordUserId = authorId,
                    SubjectPersonKey = personKeyByUser.GetValueOrDefault(authorId, $"user:{authorId}"),
                    Content = summary,
                    Salience = MemberMemorySalience,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Embedding = embedding,
                    EmbeddingModel = embedding is null ? null : embeddingModel,
                }, KeepMemoriesPerPerson, cancellationToken);
                memberMemories++;
            }
        }

        await memoryService.PruneAsync(guildId, cancellationToken);
        await SetWatermarkAsync(guildId, newest);

        logger.LogInformation(
            "Memory consolidation for guild {Guild} (model {Model}): {Lines} new message(s) → {Episodic} event(s), {Snapshots} conversation snapshot(s), {MemberMemories} member memory/ies.",
            guildId, model.Model, episodicLines.Count, episodicStored, snapshots, memberMemories);
    }

    private Task SetWatermarkAsync(ulong guildId, DateTimeOffset value) =>
        settingsService.SetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryWatermark,
            value.ToUniversalTime().ToString("o"));

    private static string Cap(string s, int max) => s.Length <= max ? s : s[^max..];
}
