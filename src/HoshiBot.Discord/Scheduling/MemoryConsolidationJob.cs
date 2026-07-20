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

        var lines = new List<string>();
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
                lines.Add($"{CommanderName.Of(message.Author)}: {text}");
                if (message.CreatedAt > newest)
                    newest = message.CreatedAt;
            }
        }

        if (lines.Count == 0)
        {
            await SetWatermarkAsync(guildId, DateTimeOffset.UtcNow);
            return;
        }

        // Keep the most recent tail if the window is huge, so a burst can't blow past the model's budget.
        var conversationText = Cap(string.Join("\n", lines), MaxCandidateChars);
        var items = await extractor.ExtractAsync(model, conversationText, cancellationToken);

        var stored = 0;
        foreach (var item in items)
        {
            var embedding = await embeddingService.EmbedAsync(item.Content!, cancellationToken);
            await memoryService.AddIfNovelAsync(new GuildMemory
            {
                GuildId = guildId,
                Scope = MemoryScope.Episodic,
                Content = item.Content!,
                Salience = item.Salience,
                CreatedAt = DateTimeOffset.UtcNow,
                SourceChannelId = null,
                Embedding = embedding,
                EmbeddingModel = embedding is null ? null : embeddingService.Model,
            }, cancellationToken);
            stored++;
        }

        await memoryService.PruneAsync(guildId, cancellationToken);
        await SetWatermarkAsync(guildId, newest);

        logger.LogInformation(
            "Memory consolidation for guild {Guild} (model {Model}): {Lines} new message(s) → {Items} memory candidate(s).",
            guildId, model.Model, lines.Count, stored);
    }

    private Task SetWatermarkAsync(ulong guildId, DateTimeOffset value) =>
        settingsService.SetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryWatermark,
            value.ToUniversalTime().ToString("o"));

    private static string Cap(string s, int max) => s.Length <= max ? s : s[^max..];
}
