using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Absences;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.AiChat;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.AlertsOptIn;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.AllianceTournament;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Announcements;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.AnonymousMessaging;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.ClientRelease;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Diplomacy;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.InfiniteIncursions;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.OpsLevelRoles;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.RaidAlerts;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.RankRoles;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.RoeViolationReports;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.ServerStatus;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.ShieldReminders;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.StfcNews;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.TerritoryCapture;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Tickets;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features;

// A manually-curated registry, not reflection-discovered — matches this codebase's
// existing "12 bespoke editors, not one generic reflection-driven one" philosophy;
// genericity stays at this route-shell/registry layer only. Each entry's Title/
// Description/Icon/editor type/configured-check now lives with its own editor in
// Features/{Name}/ — this file just lists them.
public static class FeatureCatalog
{
    // Alphabetical by Title — both the Features catalog cards and the sidebar nav group just
    // iterate this array in order, so this is the one place that controls both.
    public static readonly IFeatureModule[] All =
    [
        new AbsencesFeature(),
        new AiChatFeature(),
        new AlertsOptInFeature(),
        new AllianceTournamentFeature(),
        new AnnouncementsFeature(),
        new AnonymousMessagingFeature(),
        new ClientReleaseFeature(),
        new DiplomacyFeature(),
        new InfiniteIncursionsFeature(),
        new OpsLevelRolesFeature(),
        new RaidAlertsFeature(),
        new RankRolesFeature(),
        new RoeViolationReportsFeature(),
        new ServerStatusFeature(),
        new ShieldRemindersFeature(),
        new StfcNewsFeature(),
        new TerritoryCaptureFeature(),
        new TicketsFeature(),
    ];

    public static IFeatureModule? FindBySlug(string slug) =>
        All.FirstOrDefault(f => f.Slug == slug);
}
