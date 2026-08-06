using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Processes the thread-removal queue one request at a time (rate-limit-conscious,
// matching the original bot's behavior) once a request's grace period has elapsed —
// gives requesters a window to reconsider before the thread is actually deleted.
public class ThreadCleanupJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    ChannelCooldown cooldown,
    ILogger<ThreadCleanupJob> logger) : IJob
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    public async Task Execute(IJobExecutionContext context)
    {
        var cutoff = DateTimeOffset.UtcNow - GracePeriod;

        // Oldest request that's past its grace period AND isn't in a failure cooldown.
        //
        // The cooldown filter is what stops one undeletable thread from blocking the queue: this
        // job takes a single row per run, and on failure it used to return with that row still at
        // the head — so every other queued removal behind it waited forever on a thread that was
        // never going to be deletable, with nothing recording why.
        var candidates = await db.ThreadRemovalRequests
            .Where(r => r.RequestedAt <= cutoff)
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();

        var request = candidates.FirstOrDefault(r => !cooldown.IsCoolingDown(r.ThreadId, BotAction.RemoveThread));
        if (request is null)
            return;

        try
        {
            await gatewayClient.Rest.DeleteChannelAsync(request.ThreadId);
            logger.LogInformation(
                "Removed thread {ThreadId} as requested by {RequestedBy} at {RequestedAt}",
                request.ThreadId, request.RequestedByDiscordUserId, request.RequestedAt);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            // Thread already gone — nothing left to retry, fall through to remove from the queue.
        }
        catch (RestException ex)
        {
            // Stays queued (e.g. the bot is missing "Manage Threads"), but now behind a cooldown so
            // the identical delete isn't reissued every 15 minutes, and the next run moves on to the
            // rows behind it instead of stalling on this one.
            cooldown.RecordFailure(request.ThreadId, BotAction.RemoveThread);
            logger.LogWarning(ex,
                "Failed to remove thread {ThreadId} requested by {RequestedBy}; backing off and moving on",
                request.ThreadId, request.RequestedByDiscordUserId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(request.GuildId, BotAction.RemoveThread, request.ThreadId, BotPermission.ManageThreads);
            return;
        }

        cooldown.RecordSuccess(request.ThreadId, BotAction.RemoveThread);

        db.ThreadRemovalRequests.Remove(request);
        await db.SaveChangesAsync();
    }
}
