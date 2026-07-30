using HoshiBot.Domain.Localization;

namespace HoshiBot.Web.Components.Shared;

// The one record behind every static page registry (GuildAdminPages, AllianceAdminPages, MePages,
// BotAdminPages, StfcCatalogPages, DatabaseAdminPages) — sidebar nav groups, overview card grids
// and breadcrumbs all iterate these lists, so adding a page to a registry surfaces it everywhere
// at once and the consumers can't drift apart. Each area keeps its own static class (with the
// area-specific Href signature) as the public surface; only the record shape and the
// base-path-plus-suffix href convention live here.
//
// LabelKey/DescriptionKey are message-catalog keys resolved per viewer via Label(Language)/
// Description(Language). The GlobalAdmin-only registries (BotAdminPages, StfcCatalogPages,
// DatabaseAdminPages) are deliberately out of localization scope and keep their literal English
// strings in the key slot: MessageCatalog.Format returns the key itself when it isn't in the
// catalog, so those labels render verbatim in every language without needing catalog entries.
public sealed record AdminPage(
    string LabelKey,
    string RouteSuffix,        // "" = the area's own root page (Overview); else appended to the base path
    string Icon = "",          // Open Iconic class — consumed by the overview card grids, not the sidebar
    string DescriptionKey = "",   // static card subtitle — only MePages uses it (the admin overviews compute live counts instead)
    bool ExactMatch = false,   // Overview only (NavLinkMatch.All + exact group-active test)
    bool ShowOnOverviewCard = true)  // Overview itself is excluded from its own card grid
{
    public string Label(Language language) => MessageCatalog.Format(language, LabelKey);

    public string Description(Language language) =>
        DescriptionKey.Length == 0 ? "" : MessageCatalog.Format(language, DescriptionKey);

    // The one href convention: the bare base path for the area root, base path + "/{suffix}" otherwise.
    public string Href(string basePath) =>
        RouteSuffix.Length == 0 ? basePath : $"{basePath}/{RouteSuffix}";
}
