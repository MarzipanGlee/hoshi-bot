namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Absences (AbsenceService): draft confirm flow, own-list, and the pinned report embed.
    public static class Absence
    {
        public static string ConfirmButton(Language lang) =>
            MessageCatalog.Format(lang, "Absence.ConfirmButton");

        public static string CancelButton(Language lang) =>
            MessageCatalog.Format(lang, "Absence.CancelButton");

        public static string NoUpcoming(Language lang) =>
            MessageCatalog.Format(lang, "Absence.NoUpcoming");

        // "from until to" — the template carries the per-language date format.
        public static string Range(Language lang, DateTimeOffset start, DateTimeOffset end) =>
            MessageCatalog.Format(lang, "Absence.Range", ("start", start), ("end", end));

        public static string DraftSummary(Language lang, DateTimeOffset start, DateTimeOffset end, string reason, string visibility, string notifications) =>
            MessageCatalog.Format(lang, "Absence.DraftSummary",
                ("start", start), ("end", end), ("reason", reason), ("visibility", visibility), ("notifications", notifications));

        public static string NotificationsOn(Language lang) =>
            MessageCatalog.Format(lang, "Absence.NotificationsOn");

        public static string NotificationsOff(Language lang) =>
            MessageCatalog.Format(lang, "Absence.NotificationsOff");

        public static string DraftNotFound(Language lang) =>
            MessageCatalog.Format(lang, "Absence.DraftNotFound");

        public static string OnlyReporterConfirm(Language lang) =>
            MessageCatalog.Format(lang, "Absence.OnlyReporterConfirm");

        public static string OnlyReporterCancel(Language lang) =>
            MessageCatalog.Format(lang, "Absence.OnlyReporterCancel");

        public static string OnlyReporterDelete(Language lang) =>
            MessageCatalog.Format(lang, "Absence.OnlyReporterDelete");

        public static string Saved(Language lang) =>
            MessageCatalog.Format(lang, "Absence.Saved");

        public static string Cancelled(Language lang) =>
            MessageCatalog.Format(lang, "Absence.Cancelled");

        public static string NotFound(Language lang) =>
            MessageCatalog.Format(lang, "Absence.NotFound");

        public static string Deleted(Language lang) =>
            MessageCatalog.Format(lang, "Absence.Deleted");

        // NotificationDispatcher action/hint pieces for the report refresh.
        public static string PublicReportContext(Language lang) =>
            MessageCatalog.Format(lang, "Absence.PublicReportContext");

        public static string StaffReportContext(Language lang) =>
            MessageCatalog.Format(lang, "Absence.StaffReportContext");

        public static string ActionRefresh(Language lang, string context) =>
            MessageCatalog.Format(lang, "Absence.ActionRefresh", ("context", context));

        public static string HintChannelPermission(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Absence.HintChannelPermission", ("channel", channel));

        // The pinned report embed.
        public static string BridgeFallback(Language lang) =>
            MessageCatalog.Format(lang, "Absence.BridgeFallback");

        public static string ReportIntro(Language lang, string bridge) =>
            MessageCatalog.Format(lang, "Absence.ReportIntro", ("bridge", bridge));

        public static string ReportTitle(Language lang) =>
            MessageCatalog.Format(lang, "Absence.ReportTitle");

        public static string FieldActive(Language lang) =>
            MessageCatalog.Format(lang, "Absence.FieldActive");

        public static string FieldUpcoming(Language lang) =>
            MessageCatalog.Format(lang, "Absence.FieldUpcoming");

        public static string FieldAsOf(Language lang) =>
            MessageCatalog.Format(lang, "Absence.FieldAsOf");

        public static string NoneReported(Language lang) =>
            MessageCatalog.Format(lang, "Absence.NoneReported");

        public static string MoreHidden(Language lang, int count) =>
            MessageCatalog.Format(lang, "Absence.MoreHidden", ("count", count));

        // start/end are unix seconds rendered as Discord <t:…> stamps (per-viewer local time).
        public static string TimestampRange(Language lang, long start, long end) =>
            MessageCatalog.Format(lang, "Absence.TimestampRange", ("start", start), ("end", end));

        public static string PrivateSuffix(Language lang) =>
            MessageCatalog.Format(lang, "Absence.PrivateSuffix");

        public static string VisibilityStaff(Language lang) =>
            MessageCatalog.Format(lang, "Absence.VisibilityStaff");

        public static string VisibilityPublic(Language lang) =>
            MessageCatalog.Format(lang, "Absence.VisibilityPublic");
    }
}
