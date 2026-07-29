using HoshiBot.Data;
using HoshiBot.Discord.Alerts;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Re-pings the target of each active raid alert via DM every 15 minutes, and
// auto-terminates alerts older than 24h — the safety-net behavior ported from
// terminate-raid-warnings.yag, now automatic instead of an admin-run command.
public class RaidWarningJob(HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding, GuildFeatureService featureService, LanguageResolver languageResolver) : IJob
{
    private static readonly TimeSpan AutoTerminateAfter = TimeSpan.FromHours(24);
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromMinutes(15);

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTimeOffset.UtcNow;

        var activeAlerts = await db.Alerts
            .Include(a => a.Notifications)
            .Include(a => a.StfcSystem)
            .Where(a => a.Type == AlertType.Raid && a.TerminatedAt == null)
            .ToListAsync();

        foreach (var alert in activeAlerts)
        {
            if (!await featureService.IsEnabledAsync(alert.GuildId, GuildFeature.RaidAlerts))
                continue;

            if (now - alert.TriggeredAt >= AutoTerminateAfter)
            {
                alert.TerminatedAt = now;
                continue;
            }

            var lastUserNotification = alert.Notifications
                .Where(n => n.Kind == NotificationKind.User)
                .MaxBy(n => n.SentAt);

            if (lastUserNotification is not null && now - lastUserNotification.SentAt < ReminderInterval)
                continue;

            // The reminder is a DM to the raided commander — their language.
            var targetLang = await languageResolver.ForUserAsync(alert.TargetDiscordUserId, scopeGuildId: alert.GuildId);
            var embed = await embedBranding.BuildBrandedAsync(alert.GuildId,
                Msg.Alert.RaidReminderDm(targetLang, alert.StfcSystem?.Name ?? ""),
                EmbedBranding.DangerColor);
            var messageId = await dispatcher.SendDirectMessageAsync(alert.TargetDiscordUserId, "",
                AlertService.RaidTerminateButton(alert.GuildId, alert.TargetDiscordUserId, targetLang), embed);

            alert.Notifications.Add(new AlertNotification
            {
                Kind = NotificationKind.User,
                ChannelId = alert.TargetDiscordUserId,
                MessageId = messageId,
                SentAt = now,
            });
        }

        await db.SaveChangesAsync();
    }
}
