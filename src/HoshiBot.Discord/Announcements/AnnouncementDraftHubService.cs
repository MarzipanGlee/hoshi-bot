using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Announcements;

// The standing message at the top of a draft channel explaining how to write an announcement and
// what the four reactions mean — legacy's "Ankündigungen erstellen" hub
// (static_data/menu-announcements.yag), restored.
//
// Without it the draft channel is undiscoverable: staff see a channel where the bot silently adds
// four coloured squares to whatever they post, and nothing says which square does what. Legacy
// listed them by hand in the menu's text, which is a copy that can drift; this builds the list from
// AnnouncementSeverities, the same source the reaction handler resolves clicks against, so the two
// cannot disagree.
//
// It carries no buttons. Legacy's two both failed to survive the rewrite for their own reasons: the
// read-receipt list answered "Diese Funktion ist aktuell nicht verfügbar." and returned before its
// own body, and the cleanup button pruned entries from a flat-file list that no longer exists — the
// rewrite stores announcements as rows with foreign keys.
public class AnnouncementDraftHubService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    EmbedBranding embedBranding,
    GuildFeatureSettingsService settingsService,
    ChannelCooldown cooldown,
    LanguageResolver languageResolver,
    NotificationDispatcher dispatcher,
    ILogger<AnnouncementDraftHubService> logger)
{
    // Every (guild, audience, alliance) scope with a draft channel configured. Announcements is a
    // four-audience feature, so there is no single alliance list to walk the way the absence report
    // has — the configured channels themselves are the enumeration.
    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        var scopes = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.Feature == GuildFeature.Announcements && s.Key == AnnouncementsSettingKeys.DraftChannel)
            .Select(s => new { s.GuildId, s.Audience, s.GuildAllianceId, ChannelId = s.Value })
            .ToListAsync(cancellationToken);

        foreach (var scope in scopes)
            await RefreshAsync(scope.GuildId, scope.Audience, scope.GuildAllianceId, scope.ChannelId, cancellationToken);
    }

    private async Task RefreshAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, ulong channelId, CancellationToken cancellationToken)
    {
        // A channel that just refused us is not going to accept the same post 15 minutes later.
        if (cooldown.IsCoolingDown(channelId, BotAction.RefreshAnnouncementDraftHub))
            return;

        // The hub is a public post in the draft channel, so it renders in the owning scope's
        // language rather than any one reader's.
        var lang = guildAllianceId is { } allianceId
            ? await languageResolver.ForAllianceAsync(allianceId)
            : audience is GuildAudience.Guild or GuildAudience.None or GuildAudience.Alliance
                ? await languageResolver.ForGuildAsync(guildId)
                : await languageResolver.ForAudienceAsync(guildId, audience);

        var embed = await embedBranding.BuildBrandedAsync(guildId, BuildBody(lang), title: Msg.Announce.DraftHubTitle(lang));
        var storedId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Announcements, audience, guildAllianceId, AnnouncementsSettingKeys.DraftHubMessageId);

        if (storedId is { } messageId)
        {
            try
            {
                // Edited rather than left alone so a wording or locale change reaches the channel
                // without an admin having to notice and repost it.
                await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId, m => m.Embeds = [embed], cancellationToken: cancellationToken);
                cooldown.RecordSuccess(channelId, BotAction.RefreshAnnouncementDraftHub);
                return;
            }
            catch (RestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Genuinely gone (staff cleared the channel) — fall through and post a fresh one.
            }
            catch (RestException ex)
            {
                // Any other edit failure must NOT re-post: a transient hiccup would orphan the live
                // hub with a duplicate. Same rule as AbsenceService.PostOrEditAsync, which documents
                // that exact bug as seen in the wild.
                await HandleFailureAsync(guildId, channelId, ex, "edit");
                return;
            }
        }

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties
            {
                Embeds = [embed],
                // A reference card nobody needs paging about, like the Command Bridge hub.
                Flags = MessageFlags.SuppressNotifications,
            }, cancellationToken: cancellationToken);

            cooldown.RecordSuccess(channelId, BotAction.RefreshAnnouncementDraftHub);
            await settingsService.SetSnowflakeAsync(guildId, GuildFeature.Announcements, audience, guildAllianceId, AnnouncementsSettingKeys.DraftHubMessageId, message.Id);
        }
        catch (RestException ex)
        {
            await HandleFailureAsync(guildId, channelId, ex, "post");
        }
    }

    private async Task HandleFailureAsync(ulong guildId, ulong channelId, RestException ex, string what)
    {
        cooldown.RecordFailure(channelId, BotAction.RefreshAnnouncementDraftHub);
        logger.LogWarning(ex, "Could not {What} the announcement draft hub in channel {ChannelId} for guild {GuildId}", what, channelId, guildId);

        if (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.RefreshAnnouncementDraftHub, channelId, ChannelAccessProfile.Draft.Permissions());
    }

    // The reaction legend is generated from AnnouncementSeverities rather than written into the
    // locale string, so adding or recolouring a severity updates the hub for free — and, more to the
    // point, a severity can never be listed here with an emoji the reaction handler won't accept.
    private static string BuildBody(Language lang)
    {
        var legend = string.Join('\n', AnnouncementSeverities.Ordered.Select(severity =>
            $"- {AnnouncementSeverities.Emoji(severity)} {Msg.Announce.DraftHubSeverity(lang, severity)}"));

        return $"{Msg.Announce.DraftHubIntro(lang)}\n\n{legend}\n\n{Msg.Announce.DraftHubOutro(lang)}";
    }
}
