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
    HoshiBotDbContext db,
    AiChatIndexService indexService,
    GuildFeatureService featureService,
    ILogger<AiChatIndexJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var guildIds = await featureService.GetEnabledGuildIdsAsync(GuildFeature.AiChat, cancellationToken);

        foreach (var guildId in guildIds)
        {
            try
            {
                await indexService.BackfillGuildAsync(guildId, cancellationToken);
                // Fill embeddings for newly/previously indexed rows (bounded per run) so the vector
                // leg of hybrid search catches up alongside the progressive history backfill.
                await indexService.EmbedPendingAsync(guildId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AiChat index backfill failed for guild {GuildId}", guildId);
            }
        }
    }
}
