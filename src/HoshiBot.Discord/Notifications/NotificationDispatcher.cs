using System.Collections.Concurrent;
using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Notifications;

// Shared public/DM notification fan-out for Alerts and Shield Reminders (and, per the
// original plan, reusable as-is by later phases like territory capture/announcements).
public class NotificationDispatcher(HoshiBotDbContext db, GatewayClient gatewayClient, ILogger<NotificationDispatcher> logger, EmbedBranding embedBranding)
{
    public async Task<List<(ulong ChannelId, ulong? MessageId)>> SendPublicAsync(ulong guildId, GuildAlertChannelKind kind, string content,
        ButtonProperties? terminateButton = null, EmbedProperties? embed = null)
    {
        var channels = await db.GuildAlertChannels
            .Where(c => c.GuildId == guildId && c.Kind == kind)
            .ToListAsync();

        var results = new List<(ulong, ulong?)>();

        foreach (var channel in channels)
        {
            try
            {
                var message = await gatewayClient.Rest.SendMessageAsync(channel.ChannelId,
                    new MessageProperties
                    {
                        Content = $"<@&{channel.RoleId}> {content}",
                        Embeds = embed is null ? null : [embed],
                        Components = terminateButton is null ? null : [new ActionRowProperties([terminateButton])],
                    });
                results.Add((channel.ChannelId, message.Id));
            }
            catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                logger.LogWarning("Skipped alert channel {ChannelId} for guild {GuildId}: {StatusCode}",
                    channel.ChannelId, guildId, ex.StatusCode);
                results.Add((channel.ChannelId, null));
                await NotifyAdminOfPermissionIssueAsync(guildId, "eine Alarm-Nachricht senden", $"fehlende Berechtigung in <#{channel.ChannelId}>?");
            }
        }

        return results;
    }

    public async Task EditPublicAsync(ulong channelId, ulong messageId, string content)
    {
        try
        {
            await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId,
                m => m.Content = content);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogWarning("Could not edit notification message {MessageId} in channel {ChannelId}: {StatusCode}",
                messageId, channelId, ex.StatusCode);
        }
    }

    public async Task<ulong?> SendDirectMessageAsync(ulong userId, string content, ButtonProperties? terminateButton = null, EmbedProperties? embed = null)
    {
        try
        {
            var dmChannel = await gatewayClient.Rest.GetDMChannelAsync(userId);
            var message = await gatewayClient.Rest.SendMessageAsync(dmChannel.Id,
                new MessageProperties
                {
                    Content = content,
                    Embeds = embed is null ? null : [embed],
                    Components = terminateButton is null ? null : [new ActionRowProperties([terminateButton])],
                });
            return message.Id;
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogInformation("Could not DM user {UserId}: {StatusCode}", userId, ex.StatusCode);
            return null;
        }
    }

    // Throttles repeat admin notifications for the same (guild, context) pair — this is
    // now called from recurring Quartz jobs (every 5-15 min) as well as one-shot user
    // actions (Tickets, RoE), and a persistent misconfiguration (e.g. a permanently
    // missing "Manage Threads" grant) would otherwise re-notify on every single run
    // forever. Static/process-lifetime is deliberate and sufficient here — a hobby-scale,
    // single-instance bot, and a restart is itself a reasonable point to re-notify if the
    // problem is still there.
    private static readonly TimeSpan NotifyThrottle = TimeSpan.FromHours(1);
    private static readonly ConcurrentDictionary<(ulong GuildId, string Context), DateTimeOffset> LastNotifiedAt = new();

    // Surfaces a permission/configuration problem to a human in Discord instead of only a
    // server-side log line nobody in the guild ever sees. Reactive (call this from a catch
    // block once an action has actually failed), not a pre-check — resolving effective
    // Discord permissions ourselves would be easy to get wrong; letting the real action
    // fail and reporting that is simpler and just as accurate.
    public async Task NotifyAdminOfPermissionIssueAsync(ulong guildId, string context, string missingPermissionHint)
    {
        var key = (guildId, context);
        var now = DateTimeOffset.UtcNow;
        if (LastNotifiedAt.TryGetValue(key, out var lastSent) && now - lastSent < NotifyThrottle)
            return;
        LastNotifiedAt[key] = now;

        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.AdminChannelId is not { } channelId)
        {
            logger.LogWarning("Permission issue in guild {GuildId} ({Context}, hint: {Hint}) but no AdminChannelId configured to report it to.",
                guildId, context, missingPermissionHint);
            return;
        }

        try
        {
            var embed = new EmbedProperties
            {
                Description = $"⚠️ Der Bot konnte {context} nicht ausführen ({missingPermissionHint}). Bitte Berechtigungen prüfen.",
                Color = EmbedBranding.DangerColor,
                Author = await embedBranding.BuildAuthorAsync(guildId),
                Footer = embedBranding.BuildFooter(guildId),
            };
            await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties { Embeds = [embed] });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogWarning("Could not notify admin channel {ChannelId} for guild {GuildId} about a permission issue: {StatusCode}",
                channelId, guildId, ex.StatusCode);
        }
    }
}
