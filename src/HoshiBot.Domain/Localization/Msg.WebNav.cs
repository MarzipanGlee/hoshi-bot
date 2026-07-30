namespace HoshiBot.Domain.Localization;

public static partial class Msg
{
    // Web navigation chrome ("Web.Nav.*") — sidebar/top-bar/breadcrumb/footer labels
    // shared by NavMenu, PageBreadcrumb, LandingHeader, GuildSelector, SiteFooter and
    // the layouts. The GlobalAdmin-only sections (Bot, STFC Catalog, Database) are out
    // of localization scope and keep literal English at their call sites.
    public static class WebNav
    {
        public static string Dashboard(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.Dashboard");

        public static string GuildGroup(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.GuildGroup");

        public static string AllianceGroup(Language lang, string tag) =>
            MessageCatalog.Format(lang, "Web.Nav.AllianceGroup", ("tag", tag));

        public static string Settings(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.Settings");

        public static string Features(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.Features");

        public static string SetupWizard(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.SetupWizard");

        public static string Audience(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.Audience");

        public static string PermissionCheck(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.PermissionCheck");

        public static string NavigationMenu(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.NavigationMenu");

        public static string Home(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.Home");

        public static string Manage(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.Manage");

        public static string MyArea(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.MyArea");

        public static string MyAreaTooltip(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.MyAreaTooltip");

        public static string LogIn(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.LogIn");

        public static string LogOut(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.LogOut");

        public static string SelectGuild(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.SelectGuild");

        public static string GuildsLoading(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.GuildsLoading");

        public static string BotNotAdded(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.BotNotAdded");

        public static string Feedback(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.Feedback");

        public static string JoinDiscord(Language lang) =>
            MessageCatalog.Format(lang, "Web.Nav.JoinDiscord");

        public static string Version(Language lang, string version) =>
            MessageCatalog.Format(lang, "Web.Nav.Version", ("version", version));
    }
}
