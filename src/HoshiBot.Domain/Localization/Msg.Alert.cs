namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Raid alerts, shield reminders and opt-in roles (AlertService).
    public static class Alert
    {
        public static string TerminateButton(Language lang) =>
            MessageCatalog.Format(lang, "Alert.TerminateButton");

        public static string UnknownSystem(Language lang, string system) =>
            MessageCatalog.Format(lang, "Alert.UnknownSystem", ("system", system));

        public static string AlreadyReported(Language lang, string target, string by, long time) =>
            MessageCatalog.Format(lang, "Alert.AlreadyReported", ("target", target), ("by", by), ("time", time));

        public static string FieldSystem(Language lang) =>
            MessageCatalog.Format(lang, "Alert.FieldSystem");

        public static string FieldTarget(Language lang) =>
            MessageCatalog.Format(lang, "Alert.FieldTarget");

        public static string FieldServer(Language lang) =>
            MessageCatalog.Format(lang, "Alert.FieldServer");

        public static string FieldAttacker(Language lang) =>
            MessageCatalog.Format(lang, "Alert.FieldAttacker");

        public static string FieldReported(Language lang) =>
            MessageCatalog.Format(lang, "Alert.FieldReported");

        public static string ReportedByValue(Language lang, long time, string by) =>
            MessageCatalog.Format(lang, "Alert.ReportedByValue", ("time", time), ("by", by));

        public static string PastRaidsField(Language lang) =>
            MessageCatalog.Format(lang, "Alert.PastRaidsField");

        public static string PastRaidLine(Language lang, long time, int minutes, string system) =>
            MessageCatalog.Format(lang, "Alert.PastRaidLine", ("time", time), ("minutes", minutes), ("system", system));

        public static string ServerHome(Language lang) =>
            MessageCatalog.Format(lang, "Alert.ServerHome");

        public static string ServerEnemy(Language lang) =>
            MessageCatalog.Format(lang, "Alert.ServerEnemy");

        public static string RaidTitle(Language lang) =>
            MessageCatalog.Format(lang, "Alert.RaidTitle");

        public static string RaidEmbedBody(Language lang, string target) =>
            MessageCatalog.Format(lang, "Alert.RaidEmbedBody", ("target", target));

        public static string RaidPublic(Language lang, string target) =>
            MessageCatalog.Format(lang, "Alert.RaidPublic", ("target", target));

        public static string TestChannelHint(Language lang, string channel) =>
            MessageCatalog.Format(lang, "Alert.TestChannelHint", ("channel", channel));

        public static string RaidDm(Language lang, string system) =>
            MessageCatalog.Format(lang, "Alert.RaidDm", ("system", system));

        public static string TestCompleted(Language lang) =>
            MessageCatalog.Format(lang, "Alert.TestCompleted");

        public static string RaidReported(Language lang, string target, string system) =>
            MessageCatalog.Format(lang, "Alert.RaidReported", ("target", target), ("system", system));

        public static string OnlyStaffTerminate(Language lang) =>
            MessageCatalog.Format(lang, "Alert.OnlyStaffTerminate");

        public static string NoActiveRaid(Language lang) =>
            MessageCatalog.Format(lang, "Alert.NoActiveRaid");

        public static string RaidEnded(Language lang) =>
            MessageCatalog.Format(lang, "Alert.RaidEnded");

        public static string NoStationHousing(Language lang, string system) =>
            MessageCatalog.Format(lang, "Alert.NoStationHousing", ("system", system));

        public static string BadDuration(Language lang) =>
            MessageCatalog.Format(lang, "Alert.BadDuration");

        public static string ShieldSet(Language lang, long time, string system) =>
            MessageCatalog.Format(lang, "Alert.ShieldSet", ("time", time), ("system", system));

        public static string StaffShieldLossSet(Language lang, string target, string system, long time) =>
            MessageCatalog.Format(lang, "Alert.StaffShieldLossSet", ("target", target), ("system", system), ("time", time));

        public static string MutedNotice(Language lang) =>
            MessageCatalog.Format(lang, "Alert.MutedNotice");

        public static string UnmutedNotice(Language lang) =>
            MessageCatalog.Format(lang, "Alert.UnmutedNotice");

        public static string MutedResult(Language lang, string target) =>
            MessageCatalog.Format(lang, "Alert.MutedResult", ("target", target));

        public static string UnmutedResult(Language lang, string target) =>
            MessageCatalog.Format(lang, "Alert.UnmutedResult", ("target", target));

        public static string NoShieldReminder(Language lang) =>
            MessageCatalog.Format(lang, "Alert.NoShieldReminder");

        public static string ShieldRemoved(Language lang) =>
            MessageCatalog.Format(lang, "Alert.ShieldRemoved");

        public static string OptInAlertsLabel(Language lang) =>
            MessageCatalog.Format(lang, "Alert.OptInAlertsLabel");

        public static string RoleUnavailable(Language lang) =>
            MessageCatalog.Format(lang, "Alert.RoleUnavailable");

        public static string ActionToggleRole(Language lang) =>
            MessageCatalog.Format(lang, "Alert.ActionToggleRole");

        public static string HintManageRoles(Language lang) =>
            MessageCatalog.Format(lang, "Alert.HintManageRoles");

        public static string RoleToggleFailed(Language lang) =>
            MessageCatalog.Format(lang, "Alert.RoleToggleFailed");

        public static string RoleEnabled(Language lang, string label) =>
            MessageCatalog.Format(lang, "Alert.RoleEnabled", ("label", label));

        public static string RoleDisabled(Language lang, string label) =>
            MessageCatalog.Format(lang, "Alert.RoleDisabled", ("label", label));

        // Scheduled reminder jobs (RaidWarningJob / ShieldWarningJob).
        public static string RaidReminderDm(Language lang, string system) =>
            MessageCatalog.Format(lang, "Alert.RaidReminderDm", ("system", system));

        public static string ShieldExpiring(Language lang, string system, long time) =>
            MessageCatalog.Format(lang, "Alert.ShieldExpiring", ("system", system), ("time", time));

        public static string ShieldExpiredPublic(Language lang, string target, string system) =>
            MessageCatalog.Format(lang, "Alert.ShieldExpiredPublic", ("target", target), ("system", system));

        public static string ShieldExpiredDm(Language lang, string system) =>
            MessageCatalog.Format(lang, "Alert.ShieldExpiredDm", ("system", system));
    }
}
