namespace HoshiBot.Web.Components.Shared;

// A linked alliance's own admin pages (Overview, Features, Settings) as one list, shared by the
// sidebar's per-alliance group (NavMenu) and the alliance Overview's card grid
// (Manage/Guild/Alliance/Index) — the alliance-scoped sibling of GuildAdminPages, kept identical
// in shape so the two levels stay consistent. Icon is only consumed by the Overview cards.
public sealed record AllianceAdminPage(
    string Label,
    string Icon,               // Open Iconic class — consumed by the Overview cards
    string RouteSuffix,        // "" = Overview; else appended to manage/guild/{id}/alliance/{allianceId}/
    bool ExactMatch,           // Overview only (NavLinkMatch.All so the bare route doesn't stay lit everywhere)
    bool ShowOnOverviewCard);  // Overview itself is excluded from its own card grid

public static class AllianceAdminPages
{
    public static readonly IReadOnlyList<AllianceAdminPage> All =
    [
        new("Overview", "oi-home", "", ExactMatch: true, ShowOnOverviewCard: false),
        new("Features", "oi-puzzle-piece", "features", ExactMatch: false, ShowOnOverviewCard: true),
        new("Settings", "oi-cog", "settings", ExactMatch: false, ShowOnOverviewCard: true),
    ];

    // The bare /alliance/{allianceId} path for Overview; every other page is that path + "/{suffix}".
    // Matches the routes the alliance pages declare (and AllianceAdminPageBase's canonicalization).
    public static string Href(ulong guildId, int allianceId, AllianceAdminPage page) =>
        page.RouteSuffix.Length == 0
            ? $"manage/guild/{guildId}/alliance/{allianceId}"
            : $"manage/guild/{guildId}/alliance/{allianceId}/{page.RouteSuffix}";
}
