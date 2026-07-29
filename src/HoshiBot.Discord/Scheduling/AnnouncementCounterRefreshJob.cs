using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Announcements;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps the "✅ Lesebestätigung (N)" button label in sync with the real read-receipt
// count. Unlike legacy's hourly job (which scanned up to 100 users' individual
// confirmation dicts per announcement, capped and re-entrancy-guarded), the count here
// is a single indexed COUNT(*) per announcement — nothing expensive to guard against.
public class AnnouncementCounterRefreshJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    GuildFeatureService featureService,
    AnnouncementService announcementService,
    LanguageResolver languageResolver,
    ILogger<AnnouncementCounterRefreshJob> logger) : IJob
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    public async Task Execute(IJobExecutionContext context)
    {
        var cutoff = DateTimeOffset.UtcNow - MaxAge;

        var announcements = (await db.Announcements.ToListAsync())
            .Where(a => a.SentAt >= cutoff)
            .ToList();

        foreach (var announcement in announcements)
        {
            if (!await featureService.IsEnabledAsync(announcement.GuildId, GuildFeature.Announcements))
                continue;

            var count = await db.AnnouncementReadReceipts.CountAsync(r => r.AnnouncementId == announcement.Id);
            if (count == announcement.LastKnownReadCount)
                continue;

            try
            {
                // The refreshed label re-renders in the same scope language the published post
                // used (PostLanguageAsync re-derives it from the row's audience/channel).
                var postLang = await announcementService.PostLanguageAsync(announcement);
                await gatewayClient.Rest.ModifyMessageAsync(announcement.ChannelId, announcement.MessageId,
                    m => m.Components = [new ActionRowProperties([AnnouncementService.ReadButton(announcement.Id, count, postLang)])]);
            }
            catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                logger.LogWarning("Could not refresh read-count button for announcement {AnnouncementId}: {StatusCode}",
                    announcement.Id, ex.StatusCode);
                var guildLang = await languageResolver.ForGuildAsync(announcement.GuildId);
                await dispatcher.NotifyAdminOfPermissionIssueAsync(announcement.GuildId, Msg.Announce.ActionUpdate(guildLang),
                    Msg.Announce.HintChannelPermission(guildLang, $"<#{announcement.ChannelId}>"));
            }

            announcement.LastKnownReadCount = count;
        }

        await db.SaveChangesAsync();
    }
}
