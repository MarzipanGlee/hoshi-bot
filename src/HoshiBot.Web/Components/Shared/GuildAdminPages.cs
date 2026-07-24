namespace HoshiBot.Web.Components.Shared;

// A guild's own admin pages (Overview, Audience, Features, Settings, Setup Wizard, Permission
// Check) as one list, shared by the sidebar's GUILD group (NavMenu) and the guild Overview's
// card grid (Manage/Guild/Index) so the two can't drift apart — adding a page here surfaces it
// in both. Icon is only consumed by the Overview cards; the sidebar items stay text-only.
public sealed record GuildAdminPage(
    string Label,
    string Icon,               // Open Iconic class — consumed by the Overview cards
    string RouteSuffix,        // "" = Overview; else appended to manage/guild/{id}/
    bool ExactMatch,           // Overview only (NavLinkMatch.All + exact group-active test)
    bool ShowOnOverviewCard);  // Overview itself is excluded from its own card grid

public static class GuildAdminPages
{
    public static readonly IReadOnlyList<GuildAdminPage> All =
    [
        new("Overview", "oi-home", "", ExactMatch: true, ShowOnOverviewCard: false),
        new("Audience", "oi-people", "audience", ExactMatch: false, ShowOnOverviewCard: true),
        new("Features", "oi-puzzle-piece", "features", ExactMatch: false, ShowOnOverviewCard: true),
        new("Settings", "oi-cog", "settings", ExactMatch: false, ShowOnOverviewCard: true),
        new("Setup Wizard", "oi-list-rich", "setup-wizard", ExactMatch: false, ShowOnOverviewCard: true),
        new("Permission Check", "oi-lock-locked", "permission-check", ExactMatch: false, ShowOnOverviewCard: true),
    ];

    // The bare guild path for Overview; every other page is that path + "/{suffix}". Matches the
    // routes the pages declare (and the guild-wide Features page's AudienceFeaturesHref shape).
    public static string Href(ulong guildId, GuildAdminPage page) =>
        page.RouteSuffix.Length == 0
            ? $"manage/guild/{guildId}"
            : $"manage/guild/{guildId}/{page.RouteSuffix}";
}
