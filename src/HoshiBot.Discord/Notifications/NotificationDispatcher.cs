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
public class NotificationDispatcher(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    ILogger<NotificationDispatcher> logger,
    EmbedBranding embedBranding,
    GuildFeatureService featureService)
{
    public async Task<List<(ulong ChannelId, ulong? MessageId)>> SendPublicAsync(ulong guildId, GuildAlertChannelKind kind, string content,
        ButtonProperties? terminateButton = null, EmbedProperties? embed = null)
    {
        var channels = await db.GuildAlertChannels
            .Where(c => c.GuildId == guildId && c.Kind == kind)
            .ToListAsync();

        return await SendToChannelsAsync(guildId, channels, content, terminateButton, embed);
    }

    // Same as SendPublicAsync, but gates per-row instead of once per guild: only sends to
    // channels whose own tagged Audience is enabled for feature. Used by
    // ServerStatus/InfiniteIncursions, whose GuildAlertChannel rows can span multiple audiences for
    // the same guild — disabling the feature for one audience must not silence channels
    // tagged for a different audience the guild also serves.
    public async Task<List<(ulong ChannelId, ulong? MessageId)>> SendPublicToEnabledAudiencesAsync(
        ulong guildId, GuildAlertChannelKind kind, GuildFeature feature, string content,
        ButtonProperties? terminateButton = null, EmbedProperties? embed = null)
    {
        var channels = await db.GuildAlertChannels
            .Where(c => c.GuildId == guildId && c.Kind == kind)
            .ToListAsync();

        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, feature);
        var eligible = channels.Where(c => enabledAudiences.Contains(c.Audience)).ToList();

        return await SendToChannelsAsync(guildId, eligible, content, terminateButton, embed);
    }

    private async Task<List<(ulong ChannelId, ulong? MessageId)>> SendToChannelsAsync(
        ulong guildId, List<GuildAlertChannel> channels, string content,
        ButtonProperties? terminateButton, EmbedProperties? embed)
    {
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
                await LogSkippedChannelAsync(guildId, channel.ChannelId, ex.StatusCode);
                await NotifyAdminOfPermissionIssueAsync(guildId, "eine Alarm-Nachricht senden", $"fehlende Berechtigung in <#{channel.ChannelId}>?");
            }
        }

        return results;
    }

    // Posts to an explicit set of channel ids, mentioning mentionRoleId only when set — the
    // channel-list counterpart to SendPublicAsync (which reads GuildAlertChannel rows and pings
    // each row's own role). Used by ClientRelease, whose channels live in GuildFeatureChannel and
    // whose role is per-platform, not per-channel.
    public async Task<List<(ulong ChannelId, ulong? MessageId)>> SendToChannelIdsAsync(
        ulong guildId, IReadOnlyList<ulong> channelIds, ulong? mentionRoleId, string content, EmbedProperties? embed = null)
    {
        var results = new List<(ulong, ulong?)>();
        var prefix = mentionRoleId is { } roleId ? $"<@&{roleId}> " : "";

        foreach (var channelId in channelIds)
        {
            try
            {
                var message = await gatewayClient.Rest.SendMessageAsync(channelId,
                    new MessageProperties
                    {
                        Content = $"{prefix}{content}",
                        Embeds = embed is null ? null : [embed],
                    });
                results.Add((channelId, message.Id));
            }
            catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                logger.LogWarning("Skipped feature channel {ChannelId} for guild {GuildId}: {StatusCode}",
                    channelId, guildId, ex.StatusCode);
                results.Add((channelId, null));
                await LogSkippedChannelAsync(guildId, channelId, ex.StatusCode);
                await NotifyAdminOfPermissionIssueAsync(guildId, "eine Alarm-Nachricht senden", $"fehlende Berechtigung in <#{channelId}>?");
            }
        }

        return results;
    }

    // Sends to guildId's configured AdminChannelId (GuildSettings) and returns the sent
    // message's (channelId, messageId) so a caller can persist it for later edits — unlike
    // NotifyAdminOfPermissionIssueAsync (hardcoded wording, 1h throttle keyed by a free-text
    // context string), this is generic and untimed: callers own their own dedup (e.g. a real
    // DB row), not a time window. Returns null if AdminChannelId isn't configured or the send
    // fails, so callers can just skip rather than branch on a missing setting.
    public async Task<(ulong ChannelId, ulong MessageId)?> SendToAdminChannelAsync(
        ulong guildId, string? content = null, EmbedProperties? embed = null, IReadOnlyList<ButtonProperties>? buttons = null)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.AdminChannelId is not { } channelId)
            return null;

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Content = content,
                Embeds = embed is null ? null : [embed],
                Components = buttons is null ? null : [new ActionRowProperties(buttons)],
            });
            return (channelId, message.Id);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogWarning("Could not send admin channel notification to {ChannelId} for guild {GuildId}: {StatusCode}",
                channelId, guildId, ex.StatusCode);
            return null;
        }
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

    // Like SendDirectMessageAsync above but for a DM carrying a full set of interactive rows (e.g. a
    // confirm/decline button pair) rather than a single terminate button — used by MemberOnboarding's
    // player-confirmation outreach. Returns null (and logs) if the member's DMs are closed.
    public async Task<ulong?> SendDirectMessageAsync(ulong userId, string content, IReadOnlyList<ActionRowProperties> rows, EmbedProperties? embed = null)
    {
        try
        {
            var dmChannel = await gatewayClient.Rest.GetDMChannelAsync(userId);
            var message = await gatewayClient.Rest.SendMessageAsync(dmChannel.Id,
                new MessageProperties
                {
                    Content = content,
                    Embeds = embed is null ? null : [embed],
                    Components = rows,
                });
            return message.Id;
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogInformation("Could not DM user {UserId}: {StatusCode}", userId, ex.StatusCode);
            return null;
        }
    }

    // Records a skipped (undeliverable) channel send in the guild's general activity log
    // channel (GuildSettings.LogChannelId), separate from the throttled, admin-facing
    // NotifyAdminOfPermissionIssueAsync above: this is an untimed, per-occurrence activity-log
    // line so a guild admin watching that channel sees exactly which posts didn't go out and
    // where. No-op when no LogChannelId is configured, and a Forbidden/NotFound on the log
    // channel itself is swallowed (it's very likely the same guild-wide permission gap that
    // caused the original skip).
    private async Task LogSkippedChannelAsync(ulong guildId, ulong channelId, HttpStatusCode statusCode)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.LogChannelId is not { } logChannelId)
            return;

        var reason = statusCode == HttpStatusCode.Forbidden
            ? "fehlende Berechtigung (der Bot kann dort nicht schreiben)"
            : "der Kanal wurde nicht gefunden";

        try
        {
            var embed = new EmbedProperties
            {
                Description = $"⚠️ Eine Nachricht konnte nicht in <#{channelId}> gesendet werden — {reason}. " +
                              "Bitte die Kanal-Berechtigungen des Bots prüfen (Permission Check).",
                Color = EmbedBranding.DangerColor,
                Author = await embedBranding.BuildAuthorAsync(guildId),
                Footer = embedBranding.BuildFooter(guildId),
            };
            await gatewayClient.Rest.SendMessageAsync(logChannelId, new MessageProperties { Embeds = [embed] });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogWarning("Could not write a skipped-channel entry to log channel {LogChannelId} for guild {GuildId}: {StatusCode}",
                logChannelId, guildId, ex.StatusCode);
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
                Description = $"⚠️ Der Bot konnte folgende Aktion nicht ausführen: {context} ({missingPermissionHint}). Bitte Berechtigungen prüfen.",
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
