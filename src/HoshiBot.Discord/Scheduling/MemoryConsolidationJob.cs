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

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var guildIds = await db.GuildEnabledFeatures
            .Where(f => f.Feature == GuildFeature.AiChat)
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
                logger.LogWarning(ex, "Memory consolidation failed for guild {GuildId}", guildId);
            }
        }
    }

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var enabled = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryEnabled);
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            return;

        // Recall + dedup need embeddings; without them a memory can't be found again, so don't form any.
        if (!embeddingService.Enabled)
        {
            logger.LogInformation("Memory consolidation for guild {GuildId} skipped: embeddings disabled.", guildId);
            return;
        }

        var model = await modelResolver.ResolveLightweightAsync(guildId);
        if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
            return;

        // Watermark: only distil messages newer than the last run; first run looks back a day to bootstrap.
        var watermarkText = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryWatermark);
        var watermark = DateTimeOffset.TryParse(watermarkText, out var parsed) ? parsed : DateTimeOffset.UtcNow.AddDays(-1);

        var channels = await channelService.GetEnabledAudienceChannelsAsync(guildId, GuildFeature.AiChat);
        if (channels.Count == 0)
            return;

        // Gather new messages since the watermark: pooled (for guild-wide episodic distillation) and
        // grouped per channel (for the per-channel conversation snapshots).
        var episodicLines = new List<string>();
        var perChannelLines = new Dictionary<ulong, List<string>>();
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
                var line = $"{CommanderName.Of(message.Author)}: {text}";
                episodicLines.Add(line);
                if (!perChannelLines.TryGetValue(channelId, out var channelLines))
                    perChannelLines[channelId] = channelLines = [];
                channelLines.Add(line);
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
            var embedding = await embeddingService.EmbedAsync(item.Content!, cancellationToken);
            await memoryService.AddIfNovelAsync(new GuildMemory
            {
                GuildId = guildId,
                Scope = MemoryScope.Episodic,
                Content = item.Content!,
                Salience = item.Salience,
                CreatedAt = DateTimeOffset.UtcNow,
                Embedding = embedding,
                EmbeddingModel = embedding is null ? null : embeddingService.Model,
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
            var embedding = await embeddingService.EmbedAsync(summary, cancellationToken);
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
                EmbeddingModel = embedding is null ? null : embeddingService.Model,
            }, KeepSnapshotsPerChannel, cancellationToken);
            snapshots++;
        }

        await memoryService.PruneAsync(guildId, cancellationToken);
        await SetWatermarkAsync(guildId, newest);

        logger.LogInformation(
            "Memory consolidation for guild {Guild} (model {Model}): {Lines} new message(s) → {Episodic} event(s), {Snapshots} conversation snapshot(s).",
            guildId, model.Model, episodicLines.Count, episodicStored, snapshots);
    }

    private Task SetWatermarkAsync(ulong guildId, DateTimeOffset value) =>
        settingsService.SetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryWatermark,
            value.ToUniversalTime().ToString("o"));

    private static string Cap(string s, int max) => s.Length <= max ? s : s[^max..];
}
