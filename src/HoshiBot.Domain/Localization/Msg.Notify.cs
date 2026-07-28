namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The NotificationDispatcher's own admin-facing texts: the throttled permission-issue
    // embed and the activity-log entry for skipped (undeliverable) channel sends. The
    // action/hint strings other features pass into NotifyAdminOfPermissionIssueAsync live
    // under their own feature prefixes, not here.
    public static class Notify
    {
        public static string ActionSendAlert(Language lang) =>
            MessageCatalog.Format(lang, "Notify.ActionSendAlert");

        public static string HintChannelPermission(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Notify.HintChannelPermission", ("channel", channel));

        public static string SkipReasonForbidden(Language lang) =>
            MessageCatalog.Format(lang, "Notify.SkipReasonForbidden");

        public static string SkipReasonNotFound(Language lang) =>
            MessageCatalog.Format(lang, "Notify.SkipReasonNotFound");

        public static string SkippedChannelLog(Language lang, string channel, string reason) =>
            MessageCatalog.Format(lang, "Notify.SkippedChannelLog", ("channel", channel), ("reason", reason));

        public static string PermissionIssue(Language lang, string action, string hint) =>
            MessageCatalog.Format(lang, "Notify.PermissionIssue", ("action", action), ("hint", hint));
    }
}
