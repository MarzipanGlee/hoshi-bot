using HoshiBot.Domain.Entities;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Absences;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AiBackend;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AiChat;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AlertsOptIn;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AllianceTournament;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AnnouncementForwarder;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Announcements;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.AnonymousMessaging;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.ClientRelease;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.CommandBridge;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Diplomacy;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.HoshiSay;
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
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.ServicesRoleSync;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.ShieldReminders;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.StfcNews;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.TerritoryCapture;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.TerritoryCaptureSignOff;
using HoshiBot.Web.Components.Pages.Manage.Guild.Features.Tickets;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features;

// A manually-curated registry, not reflection-discovered — matches this codebase's
// existing "12 bespoke editors, not one generic reflection-driven one" philosophy;
// genericity stays at this route-shell/registry layer only. Each entry's Icon/editor type/
// configured-check lives with its own editor in Features/{Name}/ (titles/descriptions come
// from the message catalog via IFeatureModule's default methods) — this file just lists them.
public static class FeatureCatalog
{
    // Source order is NOT a display order: titles are per-language now, so display surfaces
    // (the Features catalog cards, the landing page) sort by Title(lang) at render time.
    public static readonly IFeatureModule[] All =
    [
        new AbsencesFeature(),
        new AiChatFeature(),
        new AiBackendFeature(),
        new AlertsOptInFeature(),
        new AllianceTournamentFeature(),
        new AnnouncementForwarderFeature(),
        new AnnouncementsFeature(),
        new AnonymousMessagingFeature(),
        new ClientReleaseFeature(),
        new CommandBridgeFeature(),
        new DiplomacyFeature(),
        new HoshiSayFeature(),
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
        new ServicesRoleSyncFeature(),
        new ShieldRemindersFeature(),
        new StfcNewsFeature(),
        new TerritoryCaptureFeature(),
        new TerritoryCaptureSignOffFeature(),
        new TicketsFeature(),
    ];

    public static IFeatureModule? FindBySlug(string slug) =>
        All.FirstOrDefault(f => f.Slug == slug);

    // The real, toggleable features relevant to an audience. Feature-count cards use this for
    // their denominator and filter their enabled numerator through the returned set — the
    // storage-only AiChatKnowledge* pseudo-features have no catalog entry, so counting enabled
    // rows unfiltered would make a fully-configured alliance read as "21/18" (18 real features
    // + 3 knowledge-tier rows).
    public static HashSet<GuildFeature> RelevantTo(GuildAudience audience) =>
        All.Where(f => f.RelevantAudiences().HasFlag(audience))
            .Select(f => f.Feature)
            .ToHashSet();

    // Resolve the module for a GuildFeature — used to turn a declared FeatureDependency into
    // its Title/Slug/Configure link. Returns null for the storage-only pseudo-features
    // (AiChatKnowledge*) that have no IFeatureModule; every dependency in the declared map
    // points at a real, toggleable feature, so it resolves for all of those.
    public static IFeatureModule? FindByFeature(GuildFeature feature) =>
        All.FirstOrDefault(f => f.Feature == feature);
}
