using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.CommandBridge;

public enum CommandBridgePublishResult
{
    // No channel is configured for this bridge — nothing was posted.
    NoChannel,

    // An existing hub message was edited in place.
    Updated,

    // A new hub message was posted (first time, or the old one was gone).
    Posted,
}

// Builds and (re)posts a guild's Command Bridge hub message for a given bridge. The single
// publish path, shared by the /post-command-bridge slash command and the Web-triggered
// CommandBridgeRepublishJob. Buttons come from the shared CommandBridgeCatalog, gated by
// per-guild feature enablement (GuildFeatureService) — the same visibility rule the Web
// overview mirrors.
public class CommandBridgeHubService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    GuildAllianceService allianceService)
{
    private const string HubDescription =
        "Commander, ich stehe zu Deinen Diensten! Wähle die gewünschte Aktion mit Hilfe der Schaltflächen.\n\nWie lautet Dein Befehl, Commander?";

    public async Task<CommandBridgePublishResult> PublishAsync(ulong guildId, CommandBridgeKind bridge)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings is null || ChannelId(settings, bridge) is not { } channelId)
            return CommandBridgePublishResult.NoChannel;

        var embed = new EmbedProperties
        {
            Title = Title(bridge),
            Description = HubDescription,
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };

        var components = await BuildComponentsAsync(guildId, bridge);

        if (MessageId(settings, bridge) is { } messageId)
        {
            try
            {
                await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId, m =>
                {
                    m.Embeds = [embed];
                    m.Components = components;
                });
                return CommandBridgePublishResult.Updated;
            }
            catch (RestException)
            {
                // Message was deleted or otherwise unreachable — fall through and re-post.
            }
        }

        var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
        {
            Embeds = [embed],
            Components = components,
        });

        SetMessageId(settings, bridge, message.Id);
        await db.SaveChangesAsync();

        return CommandBridgePublishResult.Posted;
    }

    private async Task<IMessageComponentProperties[]> BuildComponentsAsync(ulong guildId, CommandBridgeKind bridge)
    {
        var disabled = await featureService.GetDisabledAsync(guildId);

        // Group the bridge's catalog buttons by row, filtering out disabled features and
        // expanding the contact-staff button into one per configured audience.
        var rows = new List<ActionRowProperties>();
        foreach (var group in CommandBridgeCatalog.ForBridge(bridge).GroupBy(b => b.Row).OrderBy(g => g.Key))
        {
            var buttons = new List<ButtonProperties>();
            foreach (var entry in group)
            {
                if (entry.Kind == CommandBridgeButtonKind.ContactStaff)
                {
                    buttons.AddRange(await BuildContactStaffButtonsAsync(guildId));
                    continue;
                }

                if (entry.Feature is { } feature && disabled.Contains(feature))
                    continue;

                buttons.Add(new ButtonProperties(entry.CustomId, entry.LabelDe, EmojiProperties.Standard(entry.Emoji), ButtonStyle.Primary));
            }

            if (buttons.Count > 0)
                rows.Add(new ActionRowProperties(buttons));
        }

        return rows.Cast<IMessageComponentProperties>().ToArray();
    }

    // "Configured" for this button = has a channel set AND is enabled for that audience — a
    // guild with 2+ audiences gets one button per configured audience instead of one generic
    // button, so members pick the right one directly rather than being asked again in
    // ContactCommandStaffPrompt. Ported verbatim from the old CommandBridgeAdminModule.
    private async Task<List<ButtonProperties>> BuildContactStaffButtonsAsync(ulong guildId)
    {
        var relevant = GuildFeatureAudiences.RelevantAudiences(GuildFeature.Tickets); // same set as AnonymousMessaging
        var configured = new List<GuildAudience>();

        var primaryAllianceId = await allianceService.GetPrimaryIdAsync(guildId);

        foreach (var audience in GuildFeatureAudiences.EnumerateFlags(relevant))
        {
            var guildAllianceId = audience == GuildAudience.Alliance ? primaryAllianceId : null;
            if (audience == GuildAudience.Alliance && guildAllianceId is null)
                continue;

            var ticketsReady = await featureService.IsEnabledAsync(guildId, GuildFeature.Tickets, audience, guildAllianceId)
                && await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Tickets, audience, guildAllianceId, TicketsSettingKeys.Channel) is not null;
            var anonymousReady = await featureService.IsEnabledAsync(guildId, GuildFeature.AnonymousMessaging, audience, guildAllianceId)
                && await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AnonymousMessaging, audience, guildAllianceId, AnonymousMessagingSettingKeys.Channel) is not null;

            if (ticketsReady || anonymousReady)
                configured.Add(audience);
        }

        return configured.Select(audience => new ButtonProperties(
            $"contact-command-staff:{audience}",
            configured.Count > 1 ? $"Führungsstab kontaktieren ({GuildFeatureService.AudienceLabel(audience)})" : "Führungsstab kontaktieren",
            EmojiProperties.Standard("📮"), ButtonStyle.Primary)).ToList();
    }

    private static string Title(CommandBridgeKind bridge) => bridge switch
    {
        CommandBridgeKind.Staff => "Kommandobrücke Führungsstab",
        CommandBridgeKind.Friends => "Kommandobrücke Freunde",
        _ => "Kommandobrücke",
    };

    private static ulong? ChannelId(GuildSettings s, CommandBridgeKind bridge) => bridge switch
    {
        CommandBridgeKind.Staff => s.StaffCommandBridgeChannelId,
        CommandBridgeKind.Friends => s.FriendsCommandBridgeChannelId,
        _ => s.CommandBridgeChannelId,
    };

    private static ulong? MessageId(GuildSettings s, CommandBridgeKind bridge) => bridge switch
    {
        CommandBridgeKind.Staff => s.StaffCommandBridgeMessageId,
        CommandBridgeKind.Friends => s.FriendsCommandBridgeMessageId,
        _ => s.CommandBridgeMessageId,
    };

    private static void SetMessageId(GuildSettings s, CommandBridgeKind bridge, ulong messageId)
    {
        switch (bridge)
        {
            case CommandBridgeKind.Staff:
                s.StaffCommandBridgeMessageId = messageId;
                break;
            case CommandBridgeKind.Friends:
                s.FriendsCommandBridgeMessageId = messageId;
                break;
            default:
                s.CommandBridgeMessageId = messageId;
                break;
        }
    }
}
