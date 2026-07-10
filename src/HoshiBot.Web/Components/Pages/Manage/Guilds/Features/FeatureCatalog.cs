using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Absences;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.AlertsOptIn;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Announcements;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.AnonymousMessaging;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Diplomacy;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Incursion;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.RaidAlerts;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.RoeViolationReports;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.ServerStatus;
using HoshiBot.Web.Components.Pages.Manage.Guilds.Features.ShieldReminders;
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
    public static readonly IFeatureModule[] All =
    [
        new AbsencesFeature(),
        new ShieldRemindersFeature(),
        new TerritoryCaptureFeature(),
        new AnnouncementsFeature(),
        new TicketsFeature(),
        new AnonymousMessagingFeature(),
        new RoeViolationReportsFeature(),
        new AlertsOptInFeature(),
        new DiplomacyFeature(),
        new RaidAlertsFeature(),
        new ServerStatusFeature(),
        new IncursionFeature(),
    ];

    public static IFeatureModule? FindBySlug(string slug) =>
        All.FirstOrDefault(f => f.Slug == slug);
}
