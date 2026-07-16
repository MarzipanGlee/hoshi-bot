using HoshiBot.Data;
using HoshiBot.Discord.CommandBridge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Drains the Command Bridge republish queue written by the Web admin panel (a Publish button
// enqueues one CommandBridgeRepublishRequest row). Web can't build/post the hub itself — it
// doesn't reference HoshiBot.Discord — so it signals the bot, which publishes here via the
// shared CommandBridgeHubService. [DisallowConcurrentExecution] so a slow post can't be
// double-processed by an overlapping tick (CLAUDE.md).
[DisallowConcurrentExecution]
public class CommandBridgeRepublishJob(
    HoshiBotDbContext db,
    CommandBridgeHubService hubService,
    ILogger<CommandBridgeRepublishJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var requests = await db.CommandBridgeRepublishRequests
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();

        if (requests.Count == 0)
            return;

        // Collapse duplicate requests for the same guild+bridge into a single publish.
        foreach (var group in requests.GroupBy(r => (r.GuildId, r.Bridge)))
        {
            try
            {
                await hubService.PublishAsync(group.Key.GuildId, group.Key.Bridge);
            }
            catch (RestException ex)
            {
                // Leave these rows queued for retry on the next run (e.g. a transient Discord
                // error or a missing-permissions issue on the target channel).
                logger.LogWarning(ex,
                    "Failed to (re)publish {Bridge} Command Bridge for guild {GuildId}; will retry next run",
                    group.Key.Bridge, group.Key.GuildId);
                continue;
            }

            db.CommandBridgeRepublishRequests.RemoveRange(group);
        }

        await db.SaveChangesAsync();
    }
}
