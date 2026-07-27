namespace HoshiBot.Web.Components.Shared;

// A guild's own admin pages (Overview, Audience, Features, Settings, Setup Wizard, Permission
// Check) as one list, shared by the sidebar's GUILD group (NavMenu) and the guild Overview's
// card grid (Manage/Guild/Index) so the two can't drift apart — adding a page here surfaces it
// in both. Icon is only consumed by the Overview cards; the sidebar items stay text-only.
public static class GuildAdminPages
{
    public static readonly IReadOnlyList<AdminPage> All =
    [
        new("Overview", "", Icon: "oi-home", ExactMatch: true, ShowOnOverviewCard: false),
        new("Audience", "audience", Icon: "oi-people"),
        new("Settings", "settings", Icon: "oi-cog"),
        new("Features", "features", Icon: "oi-puzzle-piece"),
        new("Setup Wizard", "setup-wizard", Icon: "oi-list-rich"),
        new("Permission Check", "permission-check", Icon: "oi-lock-locked"),
    ];

    // The bare guild path for Overview; every other page is that path + "/{suffix}". Matches the
    // routes the pages declare (and the guild-wide Features page's AudienceFeaturesHref shape).
    public static string Href(ulong guildId, AdminPage page) => page.Href($"manage/guild/{guildId}");
}
