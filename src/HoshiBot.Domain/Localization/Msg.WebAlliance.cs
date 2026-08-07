namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // The per-alliance admin pages ("Web.Alliance.*") — Manage/Guild/Alliance/Index.razor
    // (Overview) and Settings.razor. Components consume the layout-cascaded Language
    // ([CascadingParameter] Language Lang). The Settings page's Language card (Phase 6c) is
    // deliberately out of scope here — its CardTitle/Usage/inherited-label text stays as-is.
    public static class WebAlliance
    {
        // Overview's PageTitle/h1 fallback before the alliance tag is known (and the
        // "needs a linked alliance" hint's own h1).
        public static string PageTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.PageTitle");

        // Overview card grid — the Settings card has no live headline number, unlike Features
        // (which reuses Web.Guild.EnabledCount).
        public static string OverviewSettingsSubtitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.OverviewSettingsSubtitle");

        public static string SettingsPageTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.SettingsPageTitle");

        public static string SettingsHeading(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Alliance.SettingsHeading", ("tag", tag));

        public static string SettingsLead(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.SettingsLead");

        // Prefixed onto every channel picker whose feature exists in the legacy bot but has not
        // been ported yet. The columns and the values admins already set are deliberately kept (a
        // future port needs both) — this just stops someone configuring a channel that does nothing
        // and wondering why it is silent.
        public static string NotImplementedYet(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.NotImplementedYet");

        public static string BoardingChannelTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.BoardingChannelTitle");

        public static string BoardingChannelUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.BoardingChannelUsage");

        public static string RemindersAlliesTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.RemindersAlliesTitle");

        public static string RulesDeTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.RulesDeTitle");

        public static string RulesEnTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.RulesEnTitle");

        public static string UserNotificationsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.UserNotificationsTitle");

        public static string UserNotificationsUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.UserNotificationsUsage");



        public static string CommandStaffJobsTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.CommandStaffJobsTitle");

        public static string DefaultCategoryTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.DefaultCategoryTitle");

        public static string DefaultCategoryUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.DefaultCategoryUsage");

        public static string ServerRootPlaceholder(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.ServerRootPlaceholder");

        public static string MemberRoleTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.MemberRoleTitle");

        public static string MemberRoleUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.MemberRoleUsage");

        public static string OfficerRoleTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.OfficerRoleTitle");

        public static string DiplomatRoleTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.DiplomatRoleTitle");

        public static string BoardingRoleTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.BoardingRoleTitle");

        public static string TimezoneTitle(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.TimezoneTitle");

        public static string TimezoneUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.TimezoneUsage");

        // The LanguagePicker's Usage text on Alliance Settings — CardTitle/InheritedLabel
        // reuse Msg.WebGuild.LanguageTitle/GuildLanguage(Inherited), same as the guild and
        // audience Settings pages.
        public static string AllianceLanguageUsage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Alliance.AllianceLanguageUsage");
    }
}
