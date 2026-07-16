using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Reminds members as their shield nears expiration, then posts a public alert once
// it expires and keeps reminding until they renew (/shield-reminder), remove, or
// disable it. Mirrors send-shield-warnings.yag's 30-minute lookahead and 15/5-minute
// reminder intervals, minus the Infinite Incursions/Territory Reset overrides.
public class ShieldWarningJob(HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding, GuildFeatureService featureService) : IJob
{
    private static readonly TimeSpan LookAhead = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CloseThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CloseInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NormalInterval = TimeSpan.FromMinutes(15);

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Add(LookAhead);

        // Only reminders whose shield expires within the look-ahead window.
        var reminders = await db.ShieldReminders
            .Include(s => s.Notifications)
            .Include(s => s.StfcSystem)
            .Where(s => !s.Disabled && s.ShieldExpiration <= cutoff)
            .ToListAsync();

        foreach (var reminder in reminders)
        {
            if (!await featureService.IsEnabledAsync(reminder.GuildId, GuildFeature.ShieldReminders))
                continue;

            var remaining = reminder.ShieldExpiration - now;

            if (remaining > TimeSpan.Zero)
            {
                await SendUserReminderIfDueAsync(reminder, now,
                    remaining <= CloseThreshold ? CloseInterval : NormalInterval,
                    $"Commander, your shield in **{reminder.StfcSystem?.Name}** expires <t:{reminder.ShieldExpiration.ToUnixTimeSeconds()}:R>!");
                continue;
            }

            // Muted (set by staff) suppresses the PUBLIC alliance warning only — the private
            // DM reminders below still go out.
            if (!reminder.Muted && reminder.Notifications.All(n => n.Kind != NotificationKind.Public))
            {
                var content = $"Commander, <@{reminder.DiscordUserId}>'s shield has expired in **{reminder.StfcSystem?.Name}**! Please assist.";
                var publicEmbed = new EmbedProperties
                {
                    Description = content,
                    Color = EmbedBranding.DangerColor,
                    Author = await embedBranding.BuildAuthorAsync(reminder.GuildId),
                    Footer = embedBranding.BuildFooter(reminder.GuildId),
                };
                var publicResults = await dispatcher.SendPublicAsync(reminder.GuildId, GuildAlertChannelKind.Shield, content, embed: publicEmbed);
                foreach (var (channelId, messageId) in publicResults)
                {
                    reminder.Notifications.Add(new ShieldReminderNotification
                    {
                        Kind = NotificationKind.Public,
                        ChannelId = channelId,
                        MessageId = messageId,
                        SentAt = now,
                    });
                }
            }

            await SendUserReminderIfDueAsync(reminder, now, CloseInterval,
                $"Commander, your shield in **{reminder.StfcSystem?.Name}** has expired! Renew with /shield-reminder or dismiss with /shield-reminder-remove.");
        }

        await db.SaveChangesAsync();
    }

    private async Task SendUserReminderIfDueAsync(ShieldReminder reminder, DateTimeOffset now, TimeSpan interval, string content)
    {
        var lastUserNotification = reminder.Notifications
            .Where(n => n.Kind == NotificationKind.User)
            .MaxBy(n => n.SentAt);

        if (lastUserNotification is not null && now - lastUserNotification.SentAt < interval)
            return;

        var embed = new EmbedProperties
        {
            Description = content,
            Color = EmbedBranding.DangerColor,
            Author = await embedBranding.BuildAuthorAsync(reminder.GuildId),
            Footer = embedBranding.BuildFooter(reminder.GuildId),
        };
        var messageId = await dispatcher.SendDirectMessageAsync(reminder.DiscordUserId, "",
            AlertService.ShieldReminderTerminateButton(reminder.GuildId), embed);

        reminder.Notifications.Add(new ShieldReminderNotification
        {
            Kind = NotificationKind.User,
            ChannelId = reminder.DiscordUserId,
            MessageId = messageId,
            SentAt = now,
        });
    }
}
