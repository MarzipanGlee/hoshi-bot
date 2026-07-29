using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.AnonymousMessages;

// A one-shot modal-to-embed relay, no thread, nothing persisted — mirrors legacy exactly
// (Channels.AnonymousMessages gets the embed, attributed to "im Auftrag von einem
// Mitglied" rather than the real sender, and nothing is stored in a DB row either).
public class AnonymousMessageService(
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    EmbedBranding embedBranding,
    GuildFeatureSettingsService settingsService,
    LanguageResolver languageResolver)
{
    // callerLanguage: the sender's resolved language, passed in by the modal module (a
    // Language rather than a userId — resolving here would need the sender's id, which this
    // deliberately anonymous relay otherwise never sees). The ephemeral status strings render
    // in it; the forwarded embed renders in the target channel's owning scope's language.
    public async Task<string> SendAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, string subject, string message, Language callerLanguage)
    {
        var channelIdResult = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AnonymousMessaging, audience, guildAllianceId, AnonymousMessagingSettingKeys.Channel);
        if (channelIdResult is not { } channelId)
            return Msg.Anon.ChannelNotConfigured(callerLanguage);

        // The staff channel the message lands in is scoped by (audience, guildAllianceId):
        // per-alliance when an alliance id exists, guild-wide for Guild/None (and as the
        // fallback for an Alliance audience without an id — ForAudienceAsync would throw),
        // else per-audience.
        var scopeLanguage = guildAllianceId is { } allianceId
            ? await languageResolver.ForAllianceAsync(allianceId)
            : audience is GuildAudience.Alliance or GuildAudience.Guild or GuildAudience.None
                ? await languageResolver.ForGuildAsync(guildId)
                : await languageResolver.ForAudienceAsync(guildId, audience);

        // "im Auftrag von einem Mitglied" moves into the description (a short
        // attribution phrase, not a labeled data point) now that Author is reserved
        // for the bot's own standardized branding.
        var embed = await embedBranding.BuildBrandedAsync(guildId, Msg.Anon.Body(scopeLanguage, message), title: subject);
        embed.Timestamp = DateTimeOffset.UtcNow;

        try
        {
            await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties { Embeds = [embed] });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            var guildLanguage = await languageResolver.ForGuildAsync(guildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, Msg.Anon.ActionSend(guildLanguage), Msg.Anon.HintChannelPermission(guildLanguage, $"<#{channelId}>"));
            return Msg.Anon.SendFailed(callerLanguage);
        }

        return Msg.Anon.Sent(callerLanguage);
    }
}
