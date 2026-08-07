using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Announcements;

// Core Announcements logic — creation starts from a draft posted in the draft channel and one of
// the severity reactions the bot adds to it (see AnnouncementDraftService), since staff need
// plain-message drafting for attachments/length/template-reuse, not a modal (see the Phase 7 plan
// section for why).
public class AnnouncementService(HoshiBotDbContext db, GatewayClient gatewayClient, EmbedBranding embedBranding, GuildFeatureSettingsService settingsService,
    LanguageResolver languageResolver, GuildMemberNames memberNames, ILogger<AnnouncementService> logger)
{
    public static ButtonProperties ReadButton(int announcementId, int count, Language lang) =>
        new($"announcement-read:{announcementId}", Msg.Announce.ReadButton(lang, count), EmojiProperties.Standard(Icons.Ok), ButtonStyle.Primary);

    // The row under a published announcement: confirm you have read THIS one, and see whatever you
    // still haven't — legacy's two buttons. The second is the same action, custom id and icon as the
    // Command Bridge's own unread button, so a member meets one control in two places rather than
    // two controls that happen to do the same thing.
    //
    // Not used for the unread LIST itself (CommandBridgeButtonModule), where every row already sits
    // under a heading of unread announcements and a "show unread" button per row would be circular.
    public static ActionRowProperties PostButtons(int announcementId, int count, Language lang) => new(
    [
        ReadButton(announcementId, count, lang),
        new ButtonProperties("announcement-show-unread", Msg.Bridge.AnnouncementsUnread(lang), EmojiProperties.Standard(Icons.Unread), ButtonStyle.Primary),
    ]);

    // First line of the draft = title, remainder = body — matches legacy's exact convention.
    public static (string Title, string Body) ParseDraft(string content)
    {
        var newlineIndex = content.IndexOf('\n');
        if (newlineIndex < 0)
            return (content.Trim(), "");

        return (content[..newlineIndex].Trim(), content[(newlineIndex + 1)..].Trim());
    }

    // The published post's channel is scoped by (audience, guildAllianceId): per-alliance when an
    // alliance id exists (the publish flow resolves the primary link for the Alliance audience),
    // guild-wide for Guild/None (and as the fallback for an Alliance audience without an id —
    // ForAudienceAsync would throw), else per-audience.
    private async Task<Language> ScopeLanguageAsync(ulong guildId, GuildAudience audience, int? guildAllianceId) =>
        guildAllianceId is { } allianceId
            ? await languageResolver.ForAllianceAsync(allianceId)
            : audience is GuildAudience.Alliance or GuildAudience.Guild or GuildAudience.None
                ? await languageResolver.ForGuildAsync(guildId)
                : await languageResolver.ForAudienceAsync(guildId, audience);

    // The language a published announcement's public post (and its Read button) renders in —
    // re-derived from the stored row for the later edits (read-count button refreshes). The row
    // doesn't store the alliance id, so it's recovered by matching the row's channel back to the
    // audience-scoped Channel setting, keeping the result consistent with what PublishAsync used.
    public async Task<Language> PostLanguageAsync(Announcement announcement)
    {
        var scopes = await settingsService.FindScopesByValueAsync(
            announcement.GuildId, GuildFeature.Announcements, AnnouncementsSettingKeys.Channel, announcement.ChannelId);
        var guildAllianceId = scopes
            .Where(s => s.Audience == announcement.Audience)
            .Select(s => s.GuildAllianceId)
            .FirstOrDefault(id => id is not null);

        return await ScopeLanguageAsync(announcement.GuildId, announcement.Audience, guildAllianceId);
    }

    // Overload for callers that only have the announcement id (the Read button's edit path);
    // falls back to the guild language if the row has vanished.
    public async Task<Language> PostLanguageAsync(int announcementId, ulong guildId) =>
        await db.Announcements.FindAsync(announcementId) is { } announcement
            ? await PostLanguageAsync(announcement)
            : await languageResolver.ForGuildAsync(guildId);

    // The published announcement's embed, built here rather than inline in PublishAsync so the
    // preview staff confirm can render the REAL thing instead of a lookalike. Before this, the
    // preview was a bare title+body embed: no colour, no attribution, no severity — so the one
    // moment staff get to check an announcement showed them something the guild would never see.
    //
    // Returns the attribution alongside, because the stored row keeps it too.
    public async Task<(EmbedProperties Embed, string Attribution)> BuildAnnouncementEmbedAsync(
        ulong guildId, RestMessage draft, AnnouncementSeverity severity, Language lang)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        var (title, body) = ParseDraft(draft.Content);
        var attachmentUrls = draft.Attachments.Select(a => a.Url).ToArray();

        // Direct (🟦 in legacy) skips the "im Auftrag von {role}" attribution entirely —
        // a direct bot announcement, not staff acting through the bot — so it's resolved
        // but simply not rendered as a field below.
        var attribution = await ResolveAttributionAsync(guildId, settings?.CommandStaffRoleId, lang);

        var fields = new List<EmbedFieldProperties>
        {
            new() { Name = Msg.Announce.FieldSeverity(lang), Value = AnnouncementSeverities.Label(severity, lang), Inline = true },
        };

        // Matches legacy's exact palette (reaction-handler.yag:47-60) — Information/
        // Warning/Danger/Bot, not approximated Bootstrap colors.
        var color = severity switch
        {
            AnnouncementSeverity.Elevated => EmbedBranding.WarningColor,
            AnnouncementSeverity.High => EmbedBranding.DangerColor,
            AnnouncementSeverity.Direct => EmbedBranding.BotColor,
            _ => EmbedBranding.InformationColor,
        };

        var embed = await embedBranding.BuildBrandedAsync(guildId,
            string.IsNullOrWhiteSpace(body) ? Msg.Announce.NoBody(lang) : body,
            color,
            string.IsNullOrWhiteSpace(title) ? Msg.Announce.NoTitle(lang) : title);

        // The attribution belongs in the author line ("Hoshi Sato im Auftrag von LF-Führungsstab"),
        // which is where legacy puts it (reaction-handler.yag:44) and where it reads as one
        // statement about who is speaking. The rewrite had made it a separate "Im Auftrag von"
        // field instead — a second place saying the same thing, and one the author line contradicts
        // by naming only the bot. Direct announcements skip it entirely: 🟦 is the bot speaking for
        // itself, not staff speaking through it.
        if (severity != AnnouncementSeverity.Direct && embed.Author is { } author)
            author.Name = Msg.Announce.AuthorOnBehalfOf(lang, author.Name ?? "", attribution);

        var imageUrl = attachmentUrls.FirstOrDefault(url => draft.Attachments.First(a => a.Url == url).ContentType?.StartsWith("image/") == true);
        if (imageUrl is not null)
        {
            embed.Image = new EmbedImageProperties(imageUrl);
        }
        else if (attachmentUrls.Length > 0)
        {
            fields.Add(new EmbedFieldProperties
            {
                Name = Msg.Announce.FieldAttachments(lang),
                Value = string.Join('\n', attachmentUrls.Select((url, i) => Msg.Announce.AttachmentLink(lang, i + 1, url))),
            });
        }

        // Legacy's "Anmerkungen" line, explaining what the read-receipt button below the post is
        // for — a bare ✅ with a number next to it doesn't say that clicking it is the point.
        fields.Add(new EmbedFieldProperties { Name = Msg.Announce.FieldRemarks(lang), Value = Msg.Announce.RemarksReadReceipt(lang) });

        embed.Fields = fields;
        embed.Timestamp = DateTimeOffset.UtcNow;
        return (embed, attribution);
    }

    // (Published, Message): the caller has to know whether it worked, because a successful publish
    // is what licenses deleting the draft and its prompt. A bare string can't say that.
    public async Task<(bool Published, string Message)> PublishAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, RestMessage draft, AnnouncementSeverity severity, ulong triggeredByUserId, bool toTestChannel = false)
    {
        // The status strings replace the publish prompt in the draft channel and are addressed to
        // the staff member who triggered it — their language; the published post itself renders in
        // its target channel's owning scope.
        var callerLang = await languageResolver.ForUserAsync(triggeredByUserId, scopeGuildId: guildId);

        // A test publish is a real publish into a different channel — same embed, same read button,
        // same stored row. Anything less isn't a rehearsal of what will happen.
        var channelKey = toTestChannel ? AnnouncementsSettingKeys.TestChannel : AnnouncementsSettingKeys.Channel;
        var channelId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Announcements, audience, guildAllianceId, channelKey);
        if (channelId is not { } channelIdValue)
            return (false, Msg.Announce.ChannelNotConfigured(callerLang));

        var scopeLang = await ScopeLanguageAsync(guildId, audience, guildAllianceId);

        var (embed, attribution) = await BuildAnnouncementEmbedAsync(guildId, draft, severity, scopeLang);
        var (title, body) = ParseDraft(draft.Content);
        var attachmentUrls = draft.Attachments.Select(a => a.Url).ToArray();

        var mentionRoleId = severity switch
        {
            // The absence-clean notification role is per-alliance (owned by the Absences feature).
            // An Elevated announcement can only ping it when it targets a specific linked alliance;
            // Server/VeilGroup/Community audiences have no single alliance role to ping.
            AnnouncementSeverity.Elevated => audience == GuildAudience.Alliance && guildAllianceId is not null
                ? await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.Absences, GuildAudience.Alliance, guildAllianceId, AbsencesSettingKeys.NotificationRole)
                : null,
            AnnouncementSeverity.High => await settingsService.GetSnowflakeAsync(
                guildId, GuildFeature.Announcements, audience, guildAllianceId, AnnouncementsSettingKeys.WarningsRole),
            _ => null,
        };

        var content = mentionRoleId is { } roleId ? $"<@&{roleId}>" : null;
        var buttons = PostButtons(0, 0, scopeLang); // placeholder id, replaced after the row is created

        var message = await gatewayClient.Rest.SendMessageAsync(channelIdValue, new MessageProperties
        {
            Content = content,
            Embeds = [embed],
            Components = [buttons],
        });

        var announcement = new Announcement
        {
            GuildId = guildId,
            ChannelId = channelIdValue,
            MessageId = message.Id,
            Title = title,
            Body = body,
            Severity = severity,
            Audience = audience,
            MentionRoleId = mentionRoleId,
            Attribution = attribution,
            AttachmentUrls = attachmentUrls,
            TriggeredByDiscordUserId = triggeredByUserId,
            SentAt = DateTimeOffset.UtcNow,
        };
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();

        // The button's custom-id needs the real announcement Id — edit it in now that we have one.
        await gatewayClient.Rest.ModifyMessageAsync(channelIdValue, message.Id,
            m => m.Components = [PostButtons(announcement.Id, 0, scopeLang)]);

        // The clicking staff member, not the draft's author — this is the ephemeral reply to their
        // button press. Resolved through the cache because draft.Author is REST-fetched and so
        // carries no nickname; the two are usually the same person, and were both wrong before.
        return (true, Msg.Announce.Published(callerLang, await memberNames.ResolveAsync(guildId, draft.Author), $"<#{channelIdValue}>"));
    }

    // The display names behind the publish buttons — legacy named its two destinations on the
    // buttons themselves, which is the only thing that makes a dry run distinguishable from the
    // real one at a glance. Null where a channel isn't configured or isn't in the cache; the caller
    // falls back to a generic label rather than inventing a name.
    public async Task<(string? LiveName, string? TestName)> PublishTargetNamesAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        var live = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Announcements, audience, guildAllianceId, AnnouncementsSettingKeys.Channel);
        var test = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Announcements, audience, guildAllianceId, AnnouncementsSettingKeys.TestChannel);
        return (ChannelName(guildId, live), ChannelName(guildId, test));
    }

    private string? ChannelName(ulong guildId, ulong? channelId) =>
        channelId is { } id && gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild) && guild.Channels.TryGetValue(id, out var channel)
            ? channel.Name
            : null;

    public async Task<(bool WasNew, int Count)> MarkReadAsync(int announcementId, ulong guildId, ulong userId)
    {
        var now = DateTimeOffset.UtcNow;

        if (await db.DiscordUsers.FindAsync(userId) is null)
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });
        if (await db.GuildMembers.FindAsync(guildId, userId) is null)
            db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = userId, JoinedAt = now });

        var existing = await db.AnnouncementReadReceipts
            .FirstOrDefaultAsync(r => r.AnnouncementId == announcementId && r.GuildId == guildId && r.DiscordUserId == userId);

        var wasNew = existing is null;
        if (wasNew)
        {
            db.AnnouncementReadReceipts.Add(new AnnouncementReadReceipt
            {
                AnnouncementId = announcementId,
                GuildId = guildId,
                DiscordUserId = userId,
                ReadAt = now,
            });
            await db.SaveChangesAsync();
        }

        var count = await db.AnnouncementReadReceipts.CountAsync(r => r.AnnouncementId == announcementId);
        return (wasNew, count);
    }

    public async Task<List<Announcement>> GetUnreadAsync(ulong guildId, ulong userId, int limit = 10) =>
        await db.Announcements
            .Where(a => a.GuildId == guildId && !a.ReadReceipts.Any(r => r.GuildId == guildId && r.DiscordUserId == userId))
            .OrderBy(a => a.SentAt)
            .Take(limit)
            .ToListAsync();

    // Just the role name now — the bot's own identity is already covered by the embed's
    // standardized Author (EmbedBranding), no longer folded into this string. The fallback
    // renders in the post's scope language — it's stored on the row and shown on the post.
    private async Task<string> ResolveAttributionAsync(ulong guildId, ulong? commandStaffRoleId, Language lang)
    {
        if (commandStaffRoleId is not { } roleId)
            return Msg.Announce.AttributionFallback(lang);

        try
        {
            var roles = await gatewayClient.Rest.GetGuildRolesAsync(guildId);
            return roles.FirstOrDefault(r => r.Id == roleId)?.Name ?? Msg.Announce.AttributionFallback(lang);
        }
        catch (RestException ex)
        {
            // Silently degrading the published "on behalf of" line to a generic label is the kind
            // of thing nobody reports and nobody can explain later.
            logger.LogWarning(ex, "Could not resolve the command staff role for guild {GuildId}; using the generic attribution", guildId);
            return Msg.Announce.AttributionFallback(lang);
        }
    }
}
