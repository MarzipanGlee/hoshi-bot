namespace HoshiBot.Web.Components.Shared;

// A linked alliance's own admin pages (Overview, Features, Settings) as one list, shared by the
// sidebar's per-alliance group (NavMenu) and the alliance Overview's card grid
// (Manage/Guild/Alliance/Index) — the alliance-scoped sibling of GuildAdminPages. Icon is only
// consumed by the Overview cards.
public static class AllianceAdminPages
{
    public static readonly IReadOnlyList<AdminPage> All =
    [
        new("Web.Page.Alliance.Overview", "", Icon: "oi-home", ExactMatch: true, ShowOnOverviewCard: false),
        new("Web.Page.Alliance.Settings", "settings", Icon: "oi-cog"),
        new("Web.Page.Alliance.Features", "features", Icon: "oi-puzzle-piece"),
    ];

    // The bare /alliance/{allianceId} path for Overview; every other page is that path + "/{suffix}".
    // Matches the routes the alliance pages declare (and AllianceAdminPageBase's canonicalization).
    public static string Href(ulong guildId, int allianceId, AdminPage page) =>
        page.Href($"manage/guild/{guildId}/alliance/{allianceId}");
}
