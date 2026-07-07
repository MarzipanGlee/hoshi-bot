using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Processes the thread-removal queue one request at a time (rate-limit-conscious,
// matching the original bot's behavior) once a request's grace period has elapsed —
// gives requesters a window to reconsider before the thread is actually deleted.
public class ThreadCleanupJob(HoshiBotDbContext db, GatewayClient gatewayClient, NotificationDispatcher dispatcher, ILogger<ThreadCleanupJob> logger) : IJob
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    public async Task Execute(IJobExecutionContext context)
    {
        var cutoff = DateTimeOffset.UtcNow - GracePeriod;

        // Filtered/ordered client-side: SQLite's EF Core provider can't translate
        // DateTimeOffset comparisons here, and this table is always small (a handful
        // of pending requests at most) so client evaluation is cheap either way.
        var request = (await db.ThreadRemovalRequests.ToListAsync())
            .Where(r => r.RequestedAt <= cutoff)
            .MinBy(r => r.RequestedAt);

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
            // Leave it queued for retry on the next run (e.g. the bot is missing
            // "Manage Threads" permission) instead of crashing the whole job.
            logger.LogWarning(ex,
                "Failed to remove thread {ThreadId} requested by {RequestedBy}; will retry next run",
                request.ThreadId, request.RequestedByDiscordUserId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(request.GuildId, "einen Thread entfernen",
                $"fehlende Manage-Threads-Berechtigung im Thread <#{request.ThreadId}>?");
            return;
        }

        db.ThreadRemovalRequests.Remove(request);
        await db.SaveChangesAsync();
    }
}
