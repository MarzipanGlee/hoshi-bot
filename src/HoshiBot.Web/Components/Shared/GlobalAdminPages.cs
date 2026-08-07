namespace HoshiBot.Web.Components.Shared;

// The three global-admin-only sidebar groups (Bot settings, STFC Catalog, raw Database debug
// tables) as one registry each — NavMenu iterates these for both the links and the group's
// active-href test, so a new page is a single entry here (previously each href was hand-rolled
// twice: once as markup, once in a parallel active-test array). Sidebar items are text-only, so
// no icons; list order is the sidebar's display order.
//
// These operator-only areas deliberately stay English (out of localization scope) — the entries
// put their literal English text in AdminPage's LabelKey slot and the catalog's raw-key fallback
// renders it verbatim; see the AdminPage record comment.
//
// GlobalAdminAreas at the bottom groups the three lists with the area-level identity (title, icon,
// description, base path) that used to be retyped at every call site.

// One operator-only area: its display identity plus the pages it owns. Unlike AdminPage, Title and
// Description are literal English rather than catalog keys — these areas are out of localization
// scope, so there is nothing to look up (AdminPage only routes through the catalog because the
// in-scope registries share the record).
public sealed record AdminArea(
    string Title,
    string Icon,                       // Open Iconic class — sidebar group header and dashboard card
    string Description,                // dashboard card subtitle
    string BasePath,                   // e.g. "manage/bot"
    IReadOnlyList<AdminPage> Pages)
{
    public string Href(AdminPage page) => page.Href(BasePath);

    // None of the areas has a root route (there is no /manage/bot page), so the dashboard card and
    // the area breadcrumb both point at the first page in the list.
    public string LandingHref => Href(Pages[0]);
}

// The single source of truth for the three global-admin areas, in sidebar/display order. The
// sidebar groups (NavMenu), the dashboard shortcut cards (Manage/Index) and the breadcrumbs
// (PageBreadcrumb) all iterate this — before it existed each of the three kept its own copy and
// they drifted: the dashboard was missing Database entirely and ordered the other two the other
// way round, and the breadcrumb had no Database branch at all.
public static class GlobalAdminAreas
{
    public static readonly IReadOnlyList<AdminArea> All =
    [
        new("Bot", "oi-shield", "Manage global admin access", "manage/bot", BotAdminPages.All),
        new("STFC Catalog", "oi-globe", "Regions, servers, alliances, and territories", "manage/stfc", StfcCatalogPages.All),
        new("Database", "oi-list-rich", "Raw table browser for debugging", "manage/database", DatabaseAdminPages.All),
    ];
}

public static class BotAdminPages
{
    public static readonly IReadOnlyList<AdminPage> All =
    [
        new("Global Admins", "global-admins"),
        new("Trusted Users", "trusted-users"),
        new("STFC News Settings", "stfc-news-settings"),
        new("Incursions Schedule", "incursions-schedule"),
    ];

    public static string Href(AdminPage page) => page.Href("manage/bot");
}

public static class StfcCatalogPages
{
    public static readonly IReadOnlyList<AdminPage> All =
    [
        new("Regions", "regions"),
        new("Veil Groups", "veil-groups"),
        new("Veil Group Invites", "veil-group-invites"),
        new("Servers", "servers"),
        new("Server Invites", "server-invites"),
        new("Alliances", "alliances"),
        new("Alliance Invites", "alliance-invites"),
        new("Alliance Name History", "alliance-name-history"),
        new("Diplomacy", "alliance-diplomacy"),
        new("Players", "players"),
        new("Player Name History", "player-name-history"),
        new("Systems", "systems"),
        new("Territories", "territories"),
        new("Territory Ownership", "territory-ownership"),
        new("Territory Neighbours", "territory-neighbours"),
        new("Territory Services", "territory-services"),
        new("Server Status", "server-status"),
        new("Event Status", "event-status"),
    ];

    public static string Href(AdminPage page) => page.Href("manage/stfc");
}

public static class DatabaseAdminPages
{
    public static readonly IReadOnlyList<AdminPage> All =
    [
        new("Client Releases", "client-releases"),
        new("Discord Guilds", "discord-guilds"),
        new("Discord Users", "discord-users"),
        new("Guild Members", "guild-members"),
        new("User Players", "user-players"),
        new("Pending Modal Inputs", "pending-modal-inputs"),
        new("Absences", "absences"),
        new("Thread Removal Requests", "thread-removal-requests"),
        new("Alerts", "alerts"),
        new("Alert Notifications", "alert-notifications"),
        new("Shield Reminders", "shield-reminders"),
        new("Shield Reminder Notifications", "shield-reminder-notifications"),
        new("Announcements", "announcements"),
        new("Readable Posts", "readable-posts"),
        new("Read Receipts", "read-receipts"),
        new("Tickets", "tickets"),
        new("RoE Violation Reports", "roe-violation-reports"),
        new("STFC News Posts", "stfc-news-posts"),
        new("STFC News Post Guild Messages", "stfc-news-post-guild-messages"),
        new("STFC Event Date Confirmations", "stfc-event-date-confirmations"),
    ];

    public static string Href(AdminPage page) => page.Href("manage/database");
}
