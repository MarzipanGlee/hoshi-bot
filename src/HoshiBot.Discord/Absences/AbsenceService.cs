using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
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
    GuildFeatureSettingsService settingsService)
{
    private static readonly TimeSpan DraftTtl = TimeSpan.FromMinutes(15);

    public static ButtonProperties ConfirmButton(int draftId) =>
        new($"absence-confirm:{draftId}", "Ja", EmojiProperties.Standard("✅"), ButtonStyle.Success);

    public static ButtonProperties CancelButton(int draftId) =>
        new($"absence-cancel:{draftId}", "Nein", EmojiProperties.Standard("✖️"), ButtonStyle.Danger);

    // No bold sub-heading here — the caller's embed Title and intro sentence already frame
    // this as "Deine Abwesenheiten", so this is just the bulleted list itself.
    public static string BuildOwnListText(List<Absence> absences)
    {
        if (absences.Count == 0)
            return "- Keine künftigen Abwesenheiten gefunden.";

        return string.Join('\n', absences.Select(a =>
            $"- {a.StartsAt:dd.MM. HH:mm} bis {a.EndsAt:dd.MM. HH:mm}" + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $" ({a.Reason})")));
    }

    public static StringMenuSelectOptionProperties BuildOption(Absence a)
    {
        var description = $"{VisibilityLabel(a.Visibility)}" + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $" — {a.Reason}");
        return new StringMenuSelectOptionProperties(
            $"{a.StartsAt:dd.MM. HH:mm} bis {a.EndsAt:dd.MM. HH:mm}",
            a.Id.ToString())
        {
            Description = description.Length > 100 ? description[..100] : description,
        };
    }

    public static string BuildDraftSummary(Absence draft) =>
        "**Zusammenfassung**\n" +
        $"Von: {draft.StartsAt:dd.MM.yyyy HH:mm}\n" +
        $"Bis: {draft.EndsAt:dd.MM.yyyy HH:mm}\n" +
        $"Grund: {draft.Reason ?? "-"}\n" +
        $"Sichtbarkeit: {VisibilityLabel(draft.Visibility)}\n" +
        $"Benachrichtigungen: {(draft.SuppressNotifications ? "🔔 Aus" : "🔔 Ein")}\n\n" +
        "Bestätigen?";

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

    // Used by the /absence slash command, which has no review-before-submit gap to
    // close (the command's arguments are the review) — saves straight to Confirmed,
    // no draft/confirm step.
    public async Task<Absence> CreateAsync(ulong guildId, ulong userId, DateTimeOffset startsAt, DateTimeOffset endsAt,
        string? reason, AbsenceVisibility visibility, bool suppressNotifications)
    {
        var absence = await InsertAsync(guildId, userId, startsAt, endsAt, reason, visibility, suppressNotifications, AbsenceStatus.Confirmed);
        await RefreshReportsAsync(guildId);
        return absence;
    }

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
            return "Entwurf nicht gefunden (evtl. abgelaufen).";
        if (draft.CreatedByDiscordUserId != callerId)
            return "Nur die meldende Person kann dies bestätigen.";

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

        return "Abwesenheit gespeichert.";
    }

    public async Task<string> CancelDraftAsync(int draftId, ulong callerId)
    {
        var draft = await db.Absences.FindAsync(draftId);
        if (draft is null || draft.Status != AbsenceStatus.Draft)
            return "Entwurf nicht gefunden (evtl. abgelaufen).";
        if (draft.CreatedByDiscordUserId != callerId)
            return "Nur die meldende Person kann dies abbrechen.";

        db.Absences.Remove(draft);
        await db.SaveChangesAsync();

        return "Abgebrochen.";
    }

    public async Task<string> DeleteAsync(int absenceId, ulong callerId)
    {
        var absence = await db.Absences.FindAsync(absenceId);
        if (absence is null)
            return "Abwesenheit nicht gefunden.";
        if (absence.CreatedByDiscordUserId != callerId)
            return "Nur die meldende Person kann diese Abwesenheit löschen.";

        var guildId = absence.GuildId;
        db.Absences.Remove(absence);
        await db.SaveChangesAsync();
        await RefreshReportsAsync(guildId);

        return "Abwesenheit gelöscht.";
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
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings is null)
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

        var publicChannelId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.Absences, GuildAudience.Alliance, AbsencesSettingKeys.ReportChannel);
        if (publicChannelId is { } publicChannelIdValue)
        {
            settings.AbsencesReportMessageId = await PostOrEditAsync(guildId, publicChannelIdValue, settings.AbsencesReportMessageId,
                await BuildReportEmbedAsync(guildId, active, upcoming, isStaffView: false), "die öffentliche Abwesenheiten-Übersicht");
        }

        var staffChannelId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.Absences, GuildAudience.Alliance, AbsencesSettingKeys.ReportStaffChannel);
        if (staffChannelId is { } staffChannelIdValue)
        {
            settings.AbsencesReportStaffMessageId = await PostOrEditAsync(guildId, staffChannelIdValue, settings.AbsencesReportStaffMessageId,
                await BuildReportEmbedAsync(guildId, active, upcoming, isStaffView: true), "die Führungsstab-Abwesenheiten-Übersicht");
        }

        await db.SaveChangesAsync();
    }

    private async Task<ulong?> PostOrEditAsync(ulong guildId, ulong channelId, ulong? existingMessageId, EmbedProperties embed, string context)
    {
        if (existingMessageId is { } messageId)
        {
            try
            {
                await gatewayClient.Rest.ModifyMessageAsync(channelId, messageId, m => m.Embeds = [embed]);
                return messageId;
            }
            catch (RestException)
            {
                // Message was deleted or otherwise unreachable — fall through and re-post.
            }
        }

        try
        {
            var message = await gatewayClient.Rest.SendMessageAsync(channelId, new MessageProperties { Embeds = [embed] });
            return message.Id;
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, $"{context} aktualisieren", $"fehlende Berechtigung in <#{channelId}>?");
            return null;
        }
    }

    private async Task<EmbedProperties> BuildReportEmbedAsync(ulong guildId, List<Absence> active, List<Absence> upcoming, bool isStaffView) =>
        new()
        {
            Title = "Abwesenheiten",
            Fields =
            [
                new EmbedFieldProperties { Name = "Aktuelle Abwesenheiten", Value = BuildSection(active, isStaffView) },
                new EmbedFieldProperties { Name = "Kommende Abwesenheiten", Value = BuildSection(upcoming, isStaffView) },
            ],
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
            Timestamp = DateTimeOffset.UtcNow,
        };

    // Staff view always shows full detail regardless of Visibility (staff need it for
    // coverage planning); the public view shows full detail only for Public rows and
    // folds StaffOnly rows into a bare count — best-effort inference from one screenshot
    // example, not yet confirmed against a real Visibility=Public case.
    private static string BuildSection(List<Absence> rows, bool isStaffView)
    {
        if (rows.Count == 0)
            return "Keine Abwesenheiten gemeldet.";

        var visible = isStaffView ? rows : rows.Where(a => a.Visibility == AbsenceVisibility.Public).ToList();
        var hiddenCount = rows.Count - visible.Count;

        var lines = visible.Select(DetailLine).ToList();
        if (hiddenCount > 0)
            lines.Add($"+{hiddenCount} weitere Abwesenheit(en)");

        return string.Join('\n', lines);
    }

    private static string DetailLine(Absence a) =>
        $"<@{a.DiscordUserId}> — {a.StartsAt:dd.MM. HH:mm} bis {a.EndsAt:dd.MM. HH:mm}"
        + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $" ({a.Reason})")
        + (a.SuppressNotifications ? " 🔔" : "")
        + (a.Visibility == AbsenceVisibility.StaffOnly ? " 🙂 Privat" : "");

    private static string VisibilityLabel(AbsenceVisibility visibility) => visibility switch
    {
        AbsenceVisibility.StaffOnly => "🙂 Führungsstab",
        _ => "🌐 Öffentlich",
    };
}
