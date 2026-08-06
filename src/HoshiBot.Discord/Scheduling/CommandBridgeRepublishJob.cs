using HoshiBot.Data;
using HoshiBot.Discord.CommandBridge;
using HoshiBot.Domain.Entities;
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
//
// This job runs every 5 seconds, which made it the worst offender in the codebase for Discord's
// invalid-request ban threshold: a failed publish used to leave the row queued with nothing
// recorded, so the identical doomed call went out 120 times per 10 minutes — and twice per attempt,
// until CommandBridgeHubService stopped falling through to a re-post on non-404 failures. The row
// now carries its own attempt count and backs off (ChannelCooldown.WaitAfter), which unlike an
// in-memory cooldown also survives a restart — worth having at this schedule.
[DisallowConcurrentExecution]
public class CommandBridgeRepublishJob(
    HoshiBotDbContext db,
    CommandBridgeHubService hubService,
    ILogger<CommandBridgeRepublishJob> logger) : IJob
{
    // After this many failures the request is left alone, carrying its error for the Web to show.
    // Deliberately not deleted: the admin needs to see what happened, and a fresh Publish click
    // enqueues a new row (attempt count 0) which revives the whole group.
    private const int MaxAttempts = 5;

    public async Task Execute(IJobExecutionContext context)
    {
        var requests = await db.CommandBridgeRepublishRequests
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();

        if (requests.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;

        // Collapse duplicate requests for the same alliance+bridge into a single publish.
        foreach (var group in requests.GroupBy(r => (r.GuildId, r.GuildAllianceId, r.Bridge)))
        {
            if (!ShouldAttempt(group, now))
                continue;

            try
            {
                await hubService.PublishAsync(group.Key.GuildId, group.Key.GuildAllianceId, group.Key.Bridge);
            }
            catch (RestException ex)
            {
                RecordFailure(group, now, $"{(int)ex.StatusCode} {ex.Error?.Message ?? ex.ReasonPhrase}");
                logger.LogWarning(ex,
                    "Failed to (re)publish {Bridge} Command Bridge for guild {GuildId} alliance {GuildAllianceId} (attempt {Attempt} of {Max})",
                    group.Key.Bridge, group.Key.GuildId, group.Key.GuildAllianceId, group.Max(r => r.AttemptCount), MaxAttempts);
                continue;
            }

            db.CommandBridgeRepublishRequests.RemoveRange(group);
        }

        await db.SaveChangesAsync();
    }

    // Due when the group still has attempts left and its backoff has elapsed. Read across the whole
    // group so a newly-enqueued row (attempt count 0) revives a group that an older, exhausted row
    // would otherwise keep parked — that click is the admin saying "try again".
    private static bool ShouldAttempt(IEnumerable<CommandBridgeRepublishRequest> group, DateTimeOffset now)
    {
        var live = group.Where(r => r.AttemptCount < MaxAttempts).ToList();
        if (live.Count == 0)
            return false;

        var attempts = live.Min(r => r.AttemptCount);
        if (attempts == 0)
            return true;

        var lastAttempt = live.Max(r => r.LastAttemptAt);
        return lastAttempt is not { } last || now - last >= ChannelCooldown.WaitAfter(attempts);
    }

    private static void RecordFailure(IEnumerable<CommandBridgeRepublishRequest> group, DateTimeOffset now, string error)
    {
        foreach (var request in group)
        {
            request.AttemptCount++;
            request.LastAttemptAt = now;
            request.LastError = error.Length > 500 ? error[..500] : error;
        }
    }
}
