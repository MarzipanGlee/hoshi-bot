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
    GuildFeatureSettingsService settingsService)
{
    // All strings come from the message catalog (Msg.Anon); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    public async Task<string> SendAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, string subject, string message)
    {
        var channelIdResult = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AnonymousMessaging, audience, guildAllianceId, AnonymousMessagingSettingKeys.Channel);
        if (channelIdResult is not { } channelId)
            return Msg.Anon.ChannelNotConfigured(Lang);

        // "im Auftrag von einem Mitglied" moves into the description (a short
        // attribution phrase, not a labeled data point) now that Author is reserved
        // for the bot's own standardized branding.
        var embed = await embedBranding.BuildBrandedAsync(guildId, Msg.Anon.Body(Lang, message), title: subject);
        embed.Timestamp = DateTimeOffset.UtcNow;

        try
        {
            await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties { Embeds = [embed] });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, Msg.Anon.ActionSend(Lang), Msg.Anon.HintChannelPermission(Lang, $"<#{channelId}>"));
            return Msg.Anon.SendFailed(Lang);
        }

        return Msg.Anon.Sent(Lang);
    }
}
