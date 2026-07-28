using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Announcements;

// Core Announcements logic — creation via a message-command preview (see
// AnnouncementMessageCommandModule) since staff need plain-message drafting for
// attachments/length/template-reuse, not a modal (see the Phase 7 plan section for why).
public class AnnouncementService(HoshiBotDbContext db, GatewayClient gatewayClient, EmbedBranding embedBranding, GuildFeatureSettingsService settingsService)
{
    // All strings come from the message catalog (Msg.Announce); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    public static ButtonProperties ReadButton(int announcementId, int count) =>
        new($"announcement-read:{announcementId}", Msg.Announce.ReadButton(Lang, count), EmojiProperties.Standard("✅"), ButtonStyle.Secondary);

    // First line of the draft = title, remainder = body — matches legacy's exact convention.
    public static (string Title, string Body) ParseDraft(string content)
    {
        var newlineIndex = content.IndexOf('\n');
        if (newlineIndex < 0)
            return (content.Trim(), "");

        return (content[..newlineIndex].Trim(), content[(newlineIndex + 1)..].Trim());
    }

    public async Task<string> PublishAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, RestMessage draft, AnnouncementSeverity severity, ulong triggeredByUserId)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);

        var channelId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Announcements, audience, guildAllianceId, AnnouncementsSettingKeys.Channel);
        if (channelId is not { } channelIdValue)
            return Msg.Announce.ChannelNotConfigured(Lang);

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

        // Direct (🟦 in legacy) skips the "im Auftrag von {role}" attribution entirely —
        // a direct bot announcement, not staff acting through the bot — so it's resolved
        // but simply not rendered as a field below.
        var attribution = await ResolveAttributionAsync(guildId, settings?.CommandStaffRoleId);

        var fields = new List<EmbedFieldProperties>
        {
            new() { Name = Msg.Announce.FieldSeverity(Lang), Value = SeverityLabel(severity), Inline = true },
        };
        if (severity != AnnouncementSeverity.Direct)
            fields.Add(new EmbedFieldProperties { Name = Msg.Announce.FieldOnBehalfOf(Lang), Value = attribution, Inline = true });

        // Matches legacy's exact palette (reaction-handler.yag:47-60) — Information/
        // Warning/Danger/Bot, not approximated Bootstrap colors.
        var color = severity switch
        {
            AnnouncementSeverity.Elevated => EmbedBranding.WarningColor,
            AnnouncementSeverity.High => EmbedBranding.DangerColor,
            AnnouncementSeverity.Direct => EmbedBranding.BotColor,
            _ => EmbedBranding.InformationColor,
        };
        var embed = await embedBranding.BuildBrandedAsync(guildId, body, color, title);
        embed.Fields = fields;
        embed.Timestamp = DateTimeOffset.UtcNow;

        var imageUrl = attachmentUrls.FirstOrDefault(url => draft.Attachments.First(a => a.Url == url).ContentType?.StartsWith("image/") == true);
        if (imageUrl is not null)
        {
            embed.Image = new EmbedImageProperties(imageUrl);
        }
        else if (attachmentUrls.Length > 0)
        {
            embed.Fields = embed.Fields.Append(new EmbedFieldProperties
            {
                Name = Msg.Announce.FieldAttachments(Lang),
                Value = string.Join('\n', attachmentUrls.Select((url, i) => Msg.Announce.AttachmentLink(Lang, i + 1, url))),
            });
        }

        var content = mentionRoleId is { } roleId ? $"<@&{roleId}>" : null;
        var readButton = ReadButton(0, 0); // placeholder id, replaced after the row is created

        var message = await gatewayClient.Rest.SendMessageAsync(channelIdValue, new MessageProperties
        {
            Content = content,
            Embeds = [embed],
            Components = [new ActionRowProperties([readButton])],
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
            m => m.Components = [new ActionRowProperties([ReadButton(announcement.Id, 0)])]);

        return Msg.Announce.Published(Lang, $"<#{channelIdValue}>");
    }

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
    // standardized Author (EmbedBranding), no longer folded into this string.
    private async Task<string> ResolveAttributionAsync(ulong guildId, ulong? commandStaffRoleId)
    {
        if (commandStaffRoleId is not { } roleId)
            return Msg.Announce.AttributionFallback(Lang);

        try
        {
            var roles = await gatewayClient.Rest.GetGuildRolesAsync(guildId);
            return roles.FirstOrDefault(r => r.Id == roleId)?.Name ?? Msg.Announce.AttributionFallback(Lang);
        }
        catch (RestException)
        {
            return Msg.Announce.AttributionFallback(Lang);
        }
    }

    private static string SeverityLabel(AnnouncementSeverity severity) => severity switch
    {
        AnnouncementSeverity.Elevated => Msg.Announce.SeverityElevated(Lang),
        AnnouncementSeverity.High => Msg.Announce.SeverityHigh(Lang),
        _ => Msg.Announce.SeverityNormal(Lang),
    };
}
