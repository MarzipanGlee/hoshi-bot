namespace HoshiBot.Web.Components.Shared;

// The one record behind every static page registry (GuildAdminPages, AllianceAdminPages, MePages,
// BotAdminPages, StfcCatalogPages, DatabaseAdminPages) — sidebar nav groups, overview card grids
// and breadcrumbs all iterate these lists, so adding a page to a registry surfaces it everywhere
// at once and the consumers can't drift apart. Each area keeps its own static class (with the
// area-specific Href signature) as the public surface; only the record shape and the
// base-path-plus-suffix href convention live here.
public sealed record AdminPage(
    string Label,
    string RouteSuffix,        // "" = the area's own root page (Overview); else appended to the base path
    string Icon = "",          // Open Iconic class — consumed by the overview card grids, not the sidebar
    string Description = "",   // static card subtitle — only MePages uses it (the admin overviews compute live counts instead)
    bool ExactMatch = false,   // Overview only (NavLinkMatch.All + exact group-active test)
    bool ShowOnOverviewCard = true)  // Overview itself is excluded from its own card grid
{
    // The one href convention: the bare base path for the area root, base path + "/{suffix}" otherwise.
    public string Href(string basePath) =>
        RouteSuffix.Length == 0 ? basePath : $"{basePath}/{RouteSuffix}";
}
