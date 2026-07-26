using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Periodically (re)builds the AI-chat knowledge search index for every guild with AiChat enabled:
// backfills history and forum threads the live message hook can't cover, and re-indexes recent
// messages to catch edits. Live indexing (AiChatMessageHandler) keeps brand-new messages fresh
// between runs.
//
// DisallowConcurrentExecution: the default SimpleTrigger fires an immediate first run at scheduler
// start, and a full per-guild REST scan is slow enough that a second tick could overlap — same
// first-sight-insert race class as the STFC notify jobs.
[DisallowConcurrentExecution]
public class AiChatIndexJob(
    AiChatIndexService indexService,
    GuildFeatureService featureService,
    ILogger<AiChatIndexJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        // recheckAudience null: no per-guild audience re-check — indexing runs for every guild with
        // any AiChat row; the index service itself no-ops on guilds with no enabled channels.
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.AiChat, null, logger, async guildId =>
        {
            await indexService.BackfillGuildAsync(guildId, context.CancellationToken);
            // Fill embeddings for newly/previously indexed rows (bounded per run) so the vector
            // leg of hybrid search catches up alongside the progressive history backfill.
            await indexService.EmbedPendingAsync(guildId, context.CancellationToken);
        }, context.CancellationToken);
}
