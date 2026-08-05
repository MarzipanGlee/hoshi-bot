using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Absences;

// Create/edit go through a Draft row confirmed/cancelled by ID (see ConfirmDraftAsync/
// CancelDraftAsync) instead of round-tripping free-text through a button's custom-id —
// same "save first, act on the saved row by ID" idea AlertService.ReportRaidAsync's
// "Beenden" button already uses. Delete has no draft step — picking a target from the
// StringMenu list is itself a deliberate one-shot gesture, unlike a freeform modal.
public class AbsenceService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    EmbedBranding embedBranding,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    LanguageResolver languageResolver)
{
    private static readonly TimeSpan DraftTtl = TimeSpan.FromMinutes(15);

    public static ButtonProperties ConfirmButton(int draftId, Language lang) =>
        new($"absence-confirm:{draftId}", Msg.Absence.ConfirmButton(lang), EmojiProperties.Standard("✅"), ButtonStyle.Success);

    public static ButtonProperties CancelButton(int draftId, Language lang) =>
        new($"absence-cancel:{draftId}", Msg.Absence.CancelButton(lang), EmojiProperties.Standard("✖️"), ButtonStyle.Danger);

    // No bold sub-heading here — the caller's embed Title and intro sentence already frame
    // this as "Deine Abwesenheiten", so this is just the bulleted list itself.
    public static string BuildOwnListText(List<Absence> absences, Language lang)
    {
        if (absences.Count == 0)
            return Msg.Absence.NoUpcoming(lang);

        return string.Join('\n', absences.Select(a =>
            $"- {Msg.Absence.Range(lang, a.StartsAt, a.EndsAt)}" + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $" ({a.Reason})")));
    }

    public static StringMenuSelectOptionProperties BuildOption(Absence a, Language lang)
    {
        var description = $"{VisibilityLabel(a.Visibility, lang)}" + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $" — {a.Reason}");
        return new StringMenuSelectOptionProperties(
            Msg.Absence.Range(lang, a.StartsAt, a.EndsAt),
            a.Id.ToString())
        {
            Description = description.Length > 100 ? description[..100] : description,
        };
    }

    public static string BuildDraftSummary(Absence draft, Language lang) =>
        Msg.Absence.DraftSummary(lang, draft.StartsAt, draft.EndsAt,
            draft.Reason ?? "-",
            VisibilityLabel(draft.Visibility, lang),
            draft.SuppressNotifications ? Msg.Absence.NotificationsOff(lang) : Msg.Absence.NotificationsOn(lang));

    public async Task<List<Absence>> GetOwnUpcomingAsync(ulong guildId, ulong userId)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Absences
            .Where(a => a.GuildId == guildId && a.DiscordUserId == userId && a.Status == AbsenceStatus.Confirmed
                && a.EndsAt > now)
            .OrderBy(a => a.StartsAt)
            .ToListAsync();
    }

    public Task<Absence> CreateDraftAsync(ulong guildId, ulong userId, DateTimeOffset startsAt, DateTimeOffset endsAt,
        string? reason, AbsenceVisibility visibility, bool suppressNotifications) =>
        InsertAsync(guildId, userId, startsAt, endsAt, reason, visibility, suppressNotifications, AbsenceStatus.Draft);

    private async Task<Absence> InsertAsync(ulong guildId, ulong userId, DateTimeOffset startsAt, DateTimeOffset endsAt,
        string? reason, AbsenceVisibility visibility, bool suppressNotifications, AbsenceStatus status)
    {
        var now = DateTimeOffset.UtcNow;

        if (await db.DiscordUsers.FindAsync(userId) is null)
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });
        if (await db.GuildMembers.FindAsync(guildId, userId) is null)
            db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = userId, JoinedAt = now });

        var absence = new Absence
        {
            GuildId = guildId,
            DiscordUserId = userId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Reason = reason,
            Visibility = visibility,
            SuppressNotifications = suppressNotifications,
            CreatedByDiscordUserId = userId,
            Status = status,
            CreatedAt = now,
        };
        db.Absences.Add(absence);
        await db.SaveChangesAsync();

        return absence;
    }

    // Only date/time/reason are editable here (the edit entry point jumps straight from
    // the StringMenu pick to this modal, with no visibility/notifications re-choice
    // steps like create has) — Visibility/SuppressNotifications carry over unchanged
    // from the original row.
    public async Task<Absence?> GetOwnAsync(int absenceId, ulong callerId)
    {
        var absence = await db.Absences.FindAsync(absenceId);
        return absence is not null && absence.CreatedByDiscordUserId == callerId ? absence : null;
    }

    public async Task<Absence?> CreateEditDraftAsync(int absenceId, ulong callerId, DateTimeOffset startsAt, DateTimeOffset endsAt, string? reason)
    {
        var original = await db.Absences.FindAsync(absenceId);
        if (original is null || original.CreatedByDiscordUserId != callerId)
            return null;

        var draft = new Absence
        {
            GuildId = original.GuildId,
            DiscordUserId = original.DiscordUserId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Reason = reason,
            Visibility = original.Visibility,
            SuppressNotifications = original.SuppressNotifications,
            CreatedByDiscordUserId = callerId,
            Status = AbsenceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            EditsAbsenceId = original.Id,
        };
        db.Absences.Add(draft);
        await db.SaveChangesAsync();

        return draft;
    }

    public async Task<string> ConfirmDraftAsync(int draftId, ulong callerId)
    {
        var draft = await db.Absences.FindAsync(draftId);
        if (draft is null || draft.Status != AbsenceStatus.Draft)
            return Msg.Absence.DraftNotFound(await languageResolver.ForUserAsync(callerId));

        // Every string here goes back ephemerally to whoever clicked the button.
        var callerLang = await languageResolver.ForUserAsync(callerId, scopeGuildId: draft.GuildId);
        if (draft.CreatedByDiscordUserId != callerId)
            return Msg.Absence.OnlyReporterConfirm(callerLang);

        var guildId = draft.GuildId;

        if (draft.EditsAbsenceId is { } originalId)
        {
            var original = await db.Absences.FindAsync(originalId);
            if (original is not null)
            {
                original.StartsAt = draft.StartsAt;
                original.EndsAt = draft.EndsAt;
                original.Reason = draft.Reason;
                original.Visibility = draft.Visibility;
                original.SuppressNotifications = draft.SuppressNotifications;
            }
            db.Absences.Remove(draft);
        }
        else
        {
            draft.Status = AbsenceStatus.Confirmed;
        }

        await db.SaveChangesAsync();
        await RefreshReportsAsync(guildId);

        return Msg.Absence.Saved(callerLang);
    }

    public async Task<string> CancelDraftAsync(int draftId, ulong callerId)
    {
        var draft = await db.Absences.FindAsync(draftId);
        if (draft is null || draft.Status != AbsenceStatus.Draft)
            return Msg.Absence.DraftNotFound(await languageResolver.ForUserAsync(callerId));

        var callerLang = await languageResolver.ForUserAsync(callerId, scopeGuildId: draft.GuildId);
        if (draft.CreatedByDiscordUserId != callerId)
            return Msg.Absence.OnlyReporterCancel(callerLang);

        db.Absences.Remove(draft);
        await db.SaveChangesAsync();

        return Msg.Absence.Cancelled(callerLang);
    }

    public async Task<string> DeleteAsync(int absenceId, ulong callerId)
    {
        var absence = await db.Absences.FindAsync(absenceId);
        if (absence is null)
            return Msg.Absence.NotFound(await languageResolver.ForUserAsync(callerId));

        var callerLang = await languageResolver.ForUserAsync(callerId, scopeGuildId: absence.GuildId);
        if (absence.CreatedByDiscordUserId != callerId)
            return Msg.Absence.OnlyReporterDelete(callerLang);

        var guildId = absence.GuildId;
        db.Absences.Remove(absence);
        await db.SaveChangesAsync();
        await RefreshReportsAsync(guildId);

        return Msg.Absence.Deleted(callerLang);
    }

    // An abandoned create/edit that never got confirmed or cancelled — same TTL spirit as
    // legacy's dbSetExpire-based state for its own confirm flows (15 min).
    public async Task SweepStaleDraftsAsync()
    {
        var cutoff = DateTimeOffset.UtcNow - DraftTtl;

        var staleDrafts = (await db.Absences.Where(a => a.Status == AbsenceStatus.Draft).ToListAsync())
            .Where(a => a.CreatedAt < cutoff)
            .ToList();

        if (staleDrafts.Count == 0)
            return;

        db.Absences.RemoveRange(staleDrafts);
        await db.SaveChangesAsync();
    }

    public async Task RefreshReportsAsync(ulong guildId)
    {
        // The absence list is guild-wide; the same report is posted to each linked alliance that
        // has Absences enabled, in that alliance's own channels (each with its own pinned message).
        var enabledAllianceIds = await featureService.GetEnabledAllianceIdsAsync(guildId, GuildFeature.Absences);
        if (enabledAllianceIds.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        var rows = (await db.Absences
            .Where(a => a.GuildId == guildId && a.Status == AbsenceStatus.Confirmed)
            .ToListAsync())
            .Where(a => a.EndsAt > now)
            .OrderBy(a => a.StartsAt)
            .ToList();

        var active = rows.Where(a => a.StartsAt <= now).ToList();
        var upcoming = rows.Where(a => a.StartsAt > now).ToList();

        // The context strings only ever surface inside NotifyAdmin messages, so they render
        // in the guild language, while the pinned report embeds themselves are per-alliance
        // public posts and follow each alliance's own language.
        var guildLang = await languageResolver.ForGuildAsync(guildId);

        foreach (var allianceId in enabledAllianceIds)
        {
            var allianceLang = await languageResolver.ForAllianceAsync(allianceId);

            // The Command Bridge mention is per-alliance, so build each alliance's embeds with
            // that alliance's own bridge channel.
            var publicEmbed = await BuildReportEmbedAsync(guildId, allianceId, active, upcoming, isStaffView: false, allianceLang);
            var staffEmbed = await BuildReportEmbedAsync(guildId, allianceId, active, upcoming, isStaffView: true, allianceLang);

            await RefreshOneAsync(guildId, allianceId, AbsencesSettingKeys.ReportChannel, AbsencesSettingKeys.ReportMessageId, publicEmbed);
            await RefreshOneAsync(guildId, allianceId, AbsencesSettingKeys.ReportStaffChannel, AbsencesSettingKeys.ReportStaffMessageId, staffEmbed);
        }
    }

    private async Task RefreshOneAsync(ulong guildId, int guildAllianceId, string channelKey, string messageKey, EmbedProperties embed)
    {
        var channelId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Absences, GuildAudience.Alliance, guildAllianceId, channelKey);
        if (channelId is not { } channelIdValue)
            return;

        var existingMessageId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Absences, GuildAudience.Alliance, guildAllianceId, messageKey);
        var newMessageId = await PostOrEditAsync(guildId, channelIdValue, existingMessageId, embed);
        await settingsService.SetSnowflakeAsync(guildId, GuildFeature.Absences, GuildAudience.Alliance, guildAllianceId, messageKey, newMessageId);
    }

    private async Task<ulong?> PostOrEditAsync(ulong guildId, ulong channelId, ulong? existingMessageId, EmbedProperties embed)
    {
        if (existingMessageId is { } messageId)
        {
            try
            {
                await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId, m => m.Embeds = [embed]);
                return messageId;
            }
            catch (RestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // The message (or its channel) is genuinely gone — fall through and re-post a fresh one.
            }
            catch (RestException ex)
            {
                // Any OTHER edit failure (rate limit 429, transient 5xx, or lost permission) must NOT
                // re-post: a momentary hiccup would orphan the still-present message with a duplicate
                // (seen in the wild — the report reposting instead of editing). Keep the id and let the
                // next refresh retry the edit; surface a genuine permission problem to admins.
                if (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    var guildLang = await languageResolver.ForGuildAsync(guildId);
                    await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.RefreshAbsenceReport, channelId, ChannelAccessProfile.Post.Permissions());
                }
                return messageId;
            }
        }

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties { Embeds = [embed] });
            return message.Id;
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            var guildLang = await languageResolver.ForGuildAsync(guildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.RefreshAbsenceReport, channelId, ChannelAccessProfile.Post.Permissions());
            return null;
        }
    }

    private async Task<EmbedProperties> BuildReportEmbedAsync(ulong guildId, int guildAllianceId, List<Absence> active, List<Absence> upcoming, bool isStaffView, Language lang)
    {
        // Link to this alliance's Command Bridge where members submit absences (falls back to
        // plain text if it hasn't configured one yet).
        var bridgeChannelId = await db.GuildAlliances
            .Where(ga => ga.Id == guildAllianceId)
            .Select(ga => ga.CommandBridgeChannelId)
            .FirstOrDefaultAsync();
        var bridge = bridgeChannelId is { } id ? $"<#{id}>" : Msg.Absence.BridgeFallback(lang);

        var embed = await embedBranding.BuildBrandedAsync(guildId,
            Msg.Absence.ReportIntro(lang, bridge),
            title: Msg.Absence.ReportTitle(lang));
        embed.Fields =
        [
            new EmbedFieldProperties { Name = Msg.Absence.FieldActive(lang), Value = BuildSection(active, isStaffView, lang) },
            new EmbedFieldProperties { Name = Msg.Absence.FieldUpcoming(lang), Value = BuildSection(upcoming, isStaffView, lang) },
            // In-body Discord timestamp (localized per viewer), not the embed footer — a
            // <t:…> stamp only renders in the body, and the members here span time zones.
            new EmbedFieldProperties { Name = Msg.Absence.FieldAsOf(lang), Value = $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:f>" },
        ];
        return embed;
    }

    // Staff view always shows full detail regardless of Visibility (staff need it for
    // coverage planning); the public view shows full detail only for Public rows and
    // folds StaffOnly rows into a bare count — best-effort inference from one screenshot
    // example, not yet confirmed against a real Visibility=Public case.
    private static string BuildSection(List<Absence> rows, bool isStaffView, Language lang)
    {
        if (rows.Count == 0)
            return Msg.Absence.NoneReported(lang);

        var visible = isStaffView ? rows : rows.Where(a => a.Visibility == AbsenceVisibility.Public).ToList();
        var hiddenCount = rows.Count - visible.Count;

        var lines = visible.Select(a => DetailLine(a, lang)).ToList();
        if (hiddenCount > 0)
            lines.Add(Msg.Absence.MoreHidden(lang, hiddenCount));

        return string.Join('\n', lines);
    }

    private static string DetailLine(Absence a, Language lang) =>
        // Discord <t:…> timestamps so each member sees the absence window in their own time zone,
        // rather than one fixed wall-clock string.
        $"<@{a.DiscordUserId}> — {Msg.Absence.TimestampRange(lang, a.StartsAt.ToUnixTimeSeconds(), a.EndsAt.ToUnixTimeSeconds())}"
        + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $" ({a.Reason})")
        + (a.SuppressNotifications ? " 🔔" : "")
        + (a.Visibility == AbsenceVisibility.StaffOnly ? $" {Msg.Absence.PrivateSuffix(lang)}" : "");

    private static string VisibilityLabel(AbsenceVisibility visibility, Language lang) => visibility switch
    {
        AbsenceVisibility.StaffOnly => Msg.Absence.VisibilityStaff(lang),
        _ => Msg.Absence.VisibilityPublic(lang),
    };
}
