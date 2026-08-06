using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
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

// Builds and (re)posts one linked alliance's Command Bridge hub message for a given bridge. The
// single publish path, used by the Web admin's publish action via CommandBridgeRepublishJob.
// (It was also the /post-command-bridge slash command's path, until that command was retired.) Command Bridges are per-alliance: each linked alliance has its own
// channels + posted hub messages (on GuildAlliance). Buttons come from the shared
// CommandBridgeCatalog, gated by this alliance's feature enablement (GuildFeatureService) — the
// same visibility rule the Web overview mirrors.
public class CommandBridgeHubService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    LanguageResolver languageResolver)
{
    public async Task<CommandBridgePublishResult> PublishAsync(ulong guildId, int guildAllianceId, CommandBridgeKind bridge)
    {
        // Command Bridge is a per-alliance feature — a disabled one isn't (re)posted (this gates
        // both the republish job and the /post-command-bridge command, which both funnel here).
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.CommandBridge, GuildAudience.Alliance, guildAllianceId))
            return CommandBridgePublishResult.NoChannel;

        var alliance = await db.GuildAlliances.FindAsync(guildAllianceId);
        if (alliance is null || alliance.GuildId != guildId || ChannelId(alliance, bridge) is not { } channelId)
            return CommandBridgePublishResult.NoChannel;

        // The hub is one public per-alliance message — everything on it (title, description,
        // buttons, including the audience-scoped contact buttons) renders in this alliance's
        // language. Each member's follow-up steps are ephemeral and switch to their own.
        var lang = await languageResolver.ForAllianceAsync(guildAllianceId);

        var embed = await embedBranding.BuildBrandedAsync(guildId, Msg.Bridge.HubDescription(lang), title: Title(bridge, lang));

        var components = await BuildComponentsAsync(guildId, guildAllianceId, bridge, lang);

        if (MessageId(alliance, bridge) is { } messageId)
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
            catch (RestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // The message is genuinely gone — fall through and re-post.
            }
            // Anything else (403, 429, 5xx) deliberately propagates instead of falling through.
            // Re-posting on those was wrong twice over: it turned every failed edit into a SECOND
            // Discord call — 240 invalid requests per 10 minutes on this job's 5-second schedule —
            // and on a merely transient failure it posted a duplicate hub and orphaned the live one,
            // exactly the bug AbsenceService.PostOrEditAsync documents as "seen in the wild" and
            // guards against. The two files now agree: only a 404 means re-post.
        }

        var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
        {
            Embeds = [embed],
            Components = components,
        });

        SetMessageId(alliance, bridge, message.Id);
        await db.SaveChangesAsync();

        return CommandBridgePublishResult.Posted;
    }

    private async Task<IMessageComponentProperties[]> BuildComponentsAsync(ulong guildId, int guildAllianceId, CommandBridgeKind bridge, Language lang)
    {
        // Per-alliance gating: a feature button shows on this alliance's hub if the feature is
        // enabled for this specific alliance (Alliance audience + this alliance id), or for any
        // of its non-alliance (guild-scoped) audiences.
        var enabled = await featureService.GetEnabledAsync(guildId);
        bool IsEnabledForHub(GuildFeature feature) => enabled.Any(e => e.Feature == feature
            && ((e.Audience == GuildAudience.Alliance && e.GuildAllianceId == guildAllianceId) || e.Audience != GuildAudience.Alliance));

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
                    buttons.AddRange(await BuildContactStaffButtonsAsync(guildId, guildAllianceId, lang));
                    continue;
                }

                if (entry.Feature is { } feature && !IsEnabledForHub(feature))
                    continue;

                buttons.Add(new ButtonProperties(entry.CustomId, MessageCatalog.Format(lang, entry.LabelKey), EmojiProperties.Standard(entry.Emoji), ButtonStyle.Primary));
            }

            if (buttons.Count > 0)
                rows.Add(new ActionRowProperties(buttons));
        }

        return rows.Cast<IMessageComponentProperties>().ToArray();
    }

    // "Configured" for this button = has a channel set AND is enabled for that audience — a
    // guild with 2+ audiences gets one button per configured audience instead of one generic
    // button, so members pick the right one directly rather than being asked again in
    // ContactCommandStaffPrompt. The Alliance audience uses this hub's own alliance.
    private async Task<List<ButtonProperties>> BuildContactStaffButtonsAsync(ulong guildId, int guildAllianceId, Language lang)
    {
        var relevant = GuildFeatureAudiences.RelevantAudiences(GuildFeature.Tickets); // same set as AnonymousMessaging
        var configured = new List<GuildAudience>();

        foreach (var audience in GuildFeatureAudiences.EnumerateFlags(relevant))
        {
            var allianceId = audience == GuildAudience.Alliance ? guildAllianceId : (int?)null;

            var ticketsReady = await featureService.IsEnabledAsync(guildId, GuildFeature.Tickets, audience, allianceId)
                && await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Tickets, audience, allianceId, TicketsSettingKeys.Channel) is not null;
            var anonymousReady = await featureService.IsEnabledAsync(guildId, GuildFeature.AnonymousMessaging, audience, allianceId)
                && await settingsService.GetSnowflakeAsync(guildId, GuildFeature.AnonymousMessaging, audience, allianceId, AnonymousMessagingSettingKeys.Channel) is not null;

            if (ticketsReady || anonymousReady)
                configured.Add(audience);
        }

        // The single-audience label doubles as the contact step's title — same wording, one key.
        return configured.Select(audience => new ButtonProperties(
            $"contact-command-staff:{audience}",
            configured.Count > 1 ? Msg.Bridge.ContactStaffAudience(lang, GuildFeatureService.AudienceLabel(audience, lang)) : Msg.Bridge.ContactTitle(lang),
            EmojiProperties.Standard("📮"), ButtonStyle.Primary)).ToList();
    }

    private static string Title(CommandBridgeKind bridge, Language lang) => bridge switch
    {
        CommandBridgeKind.Staff => Msg.Bridge.HubTitleStaff(lang),
        CommandBridgeKind.Friends => Msg.Bridge.HubTitleFriends(lang),
        _ => Msg.Bridge.HubTitleUser(lang),
    };

    private static ulong? ChannelId(GuildAlliance a, CommandBridgeKind bridge) => bridge switch
    {
        CommandBridgeKind.Staff => a.StaffCommandBridgeChannelId,
        CommandBridgeKind.Friends => a.FriendsCommandBridgeChannelId,
        _ => a.CommandBridgeChannelId,
    };

    private static ulong? MessageId(GuildAlliance a, CommandBridgeKind bridge) => bridge switch
    {
        CommandBridgeKind.Staff => a.StaffCommandBridgeMessageId,
        CommandBridgeKind.Friends => a.FriendsCommandBridgeMessageId,
        _ => a.CommandBridgeMessageId,
    };

    private static void SetMessageId(GuildAlliance a, CommandBridgeKind bridge, ulong messageId)
    {
        switch (bridge)
        {
            case CommandBridgeKind.Staff:
                a.StaffCommandBridgeMessageId = messageId;
                break;
            case CommandBridgeKind.Friends:
                a.FriendsCommandBridgeMessageId = messageId;
                break;
            default:
                a.CommandBridgeMessageId = messageId;
                break;
        }
    }
}
