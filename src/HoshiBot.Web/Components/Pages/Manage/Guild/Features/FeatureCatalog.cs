using HoshiBot.Domain.Entities;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Absences;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AiChat;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AlertsOptIn;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AllianceTournament;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Announcements;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AnonymousMessaging;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.ClientRelease;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Diplomacy;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.InfiniteIncursions;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.MemberLore;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.MemberOnboarding;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.NicknameSync;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.OpsLevelRoles;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.PlayerLink;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.RaidAlerts;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.RankRoles;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.RoeViolationReports;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.ServerStatus;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.ShieldReminders;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.StfcNews;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.TerritoryCapture;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Tickets;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features;

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
        new MemberLoreFeature(),
        new MemberOnboardingFeature(),
        new NicknameSyncFeature(),
        new OpsLevelRolesFeature(),
        new PlayerLinkFeature(),
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

    // Resolve the module for a GuildFeature — used to turn a declared FeatureDependency into
    // its Title/Slug/Configure link. Returns null for the storage-only pseudo-features
    // (AiChatKnowledge*) that have no IFeatureModule; every dependency in the declared map
    // points at a real, toggleable feature, so it resolves for all of those.
    public static IFeatureModule? FindByFeature(GuildFeature feature) =>
        All.FirstOrDefault(f => f.Feature == feature);
}
