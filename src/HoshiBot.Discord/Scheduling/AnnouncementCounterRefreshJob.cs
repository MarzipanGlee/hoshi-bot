using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.ReadReceipts;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps the "✅ Lesebestätigung (N)" button label in sync with the real read-receipt count, on every
// kind of tracked post rather than just announcements. Unlike legacy's hourly job (which scanned up
// to 100 users' individual confirmation dicts per announcement, capped and re-entrancy-guarded), the
// count here is a single indexed COUNT(*) per post — nothing expensive to guard against.
public class AnnouncementCounterRefreshJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    ILogger<AnnouncementCounterRefreshJob> logger) : IJob
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    public async Task Execute(IJobExecutionContext context)
    {
        var cutoff = DateTimeOffset.UtcNow - MaxAge;

        // Only tracked posts, and the flag on the row is the whole test — the feature is never
        // consulted here, so turning a kind off leaves posts that already carry a button counting
        // correctly rather than freezing them at whatever the last run saw.
        var posts = (await db.ReadablePosts.Where(p => p.ReadReceiptsEnabled).ToListAsync())
            .Where(p => p.PostedAt >= cutoff)
            .ToList();

        foreach (var post in posts)
        {
            var count = await db.ReadReceipts.CountAsync(r => r.ReadablePostId == post.Id);
            if (count == post.LastKnownReadCount)
                continue;

            try
            {
                // The post's own language, stored at registration — the text above the button is
                // frozen in it, so the label has to be too.
                await gatewayClient.Rest.ModifyMessageAsync(post.ChannelId, post.MessageId,
                    m => m.Components = [ReadReceiptService.Buttons(post.Id, count, post.Language)]);
            }
            catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                logger.LogWarning("Could not refresh read-count button for post {PostId}: {StatusCode}", post.Id, ex.StatusCode);
                await dispatcher.NotifyAdminOfPermissionIssueAsync(post.GuildId, BotAction.UpdateAnnouncement, post.ChannelId,
                    BotPermission.ViewChannel | BotPermission.SendMessages);
            }

            post.LastKnownReadCount = count;
        }

        await db.SaveChangesAsync();
    }
}
