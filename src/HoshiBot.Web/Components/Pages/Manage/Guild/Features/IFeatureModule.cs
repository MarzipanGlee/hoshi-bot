using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features;

// One implementation per GuildFeature, each living in its own Features/{Name}/ subfolder
// alongside its editor component — the single source of truth consumed by the feature
// route shell (FeatureSettings.razor), the Features page's audience-grouped card grid, and
// the sidebar's Features nav group. RelevantAudiences/HasMultipleAudiences are NOT declared
// here — see the extension methods below; they're computed from GuildFeatureAudiences
// (HoshiBot.Domain), the single source of truth also shared with GuildFeatureService/
// HoshiBot.Discord, so the two never drift apart.
public interface IFeatureModule
{
    GuildFeature Feature { get; }
    string Slug { get; }
    string Icon { get; }              // Open Iconic class, e.g. "oi-calendar"
    Type EditorComponentType { get; }

    // Viewer-language display text, resolved from the message catalog by the Feature enum
    // ("Web.Feature.<Feature>.Title"/".Description") — default interface methods so no module
    // carries display strings anymore; the catalog pair is the single source of truth.
    string Title(Language language) => Msg.WebFeature.Title(language, Feature);
    string Description(Language language) => Msg.WebFeature.Description(language, Feature);

    // A feature's own auxiliary admin pages beyond its main editor (e.g. MemberLore's notes/review
    // queue, AiChat's memories) — routed generically by FeatureExtraPage.razor at
    // /manage/guild/{GuildId}/features/{Slug}/{ExtraSlug} and auto-breadcrumbed by
    // PageBreadcrumb.razor's AddFeatureCrumbs from this same declaration. Deliberately guild-wide only
    // (no audience/alliance nesting) — these pages' underlying data (GuildMemberNote, GuildMemory, …)
    // isn't alliance-scoped even when the owning feature is. Empty for every feature that has none.
    IReadOnlyList<FeatureExtraPage> ExtraPages => [];

    // Identical logic for every feature (just checks GuildEnabledFeature for Feature+
    // audience+alliance) — a default interface method, so this is written once here rather than
    // duplicated verbatim across all module classes. guildAllianceId scopes the Alliance
    // audience to one linked alliance (null otherwise — see FeatureScopeGuard).
    Task<bool> IsEnabledAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        context.IsEnabledAsync(guildId, Feature, audience, guildAllianceId);

    // Whether this feature's required settings are actually present for guildId+audience+alliance
    // — the Features page's yellow "enabled but not configured" state. Unlike IsEnabledAsync,
    // this genuinely varies per feature (different settings shapes), so every module
    // implements its own — no default body.
    Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context);
}

// One of a feature's auxiliary admin pages — see IFeatureModule.ExtraPages. ComponentType must inherit
// GuildAdminPageBase (a [Parameter] string GuildId, same as any other guild-scoped page) —
// FeatureExtraPageHost.razor is all that ever instantiates it, and only ever passes that one parameter.
// The title comes from the catalog ("Web.Feature.<Feature>.Extra.<Slug>"); the owning feature is
// passed in by the caller (both consumers already hold the module) rather than duplicated per entry.
public record FeatureExtraPage(string Slug, Type ComponentType)
{
    public string Title(Language language, GuildFeature feature) =>
        Msg.WebFeature.ExtraTitle(language, feature, Slug);
}

// Everything a module's own checks might need, bundled so the interface members stay one
// parameter: FeatureService for IsEnabledAsync (uniform); Settings for most
// IsConfiguredAsync implementations; DbFactory for the alert-list/channel features' checks.
//
// Snapshot is an optional preloaded read of the guild's settings/enablement/channels — the Features
// page supplies one so the ~25 per-feature checks + dependency fan-out read memory instead of firing
// a query each. The read helpers below hit the snapshot when present, else fall through to the live
// services/DB (single-feature callers pass no snapshot). Modules should call these, not Settings/
// FeatureService/DbFactory directly, so they transparently get the snapshot fast-path.
public record FeatureModuleContext(
    GuildFeatureService FeatureService,
    GuildFeatureSettingsService Settings,
    IDbContextFactory<HoshiBotDbContext> DbFactory,
    FeatureSettingsSnapshot? Snapshot = null)
{
    // A shared role owned by the scope rather than by a feature (see SharedSettingUsage). Read
    // through DbFactory rather than by taking another constructor parameter — the record is built in
    // three places, and a module asking "is my scope configured" should not force all of them to
    // learn about every shared-role accessor.
    public async Task<ulong?> GetAllianceRoleAsync(ulong guildId, GuildAudience audience, int? guildAllianceId,
        Func<GuildAlliance, ulong?> select)
    {
        if (audience != GuildAudience.Alliance || guildAllianceId is not { } allianceId)
            return null;

        await using var db = await DbFactory.CreateDbContextAsync();
        var alliance = await db.GuildAlliances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.GuildId == guildId && a.Id == allianceId);
        return alliance is null ? null : select(alliance);
    }

    public Task<ulong?> GetSnowflakeAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key) =>
        Snapshot is { } s ? Task.FromResult(s.GetSnowflake(feature, audience, guildAllianceId, key))
            : Settings.GetSnowflakeAsync(guildId, feature, audience, guildAllianceId, key);

    public Task<string?> GetTextAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key) =>
        Snapshot is { } s ? Task.FromResult(s.GetText(feature, audience, guildAllianceId, key))
            : Settings.GetTextAsync(guildId, feature, audience, guildAllianceId, key);

    public Task<bool> IsEnabledAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId) =>
        Snapshot is { } s ? Task.FromResult(s.IsEnabled(feature, audience, guildAllianceId))
            : FeatureService.IsEnabledAsync(guildId, feature, audience, guildAllianceId);

    public Task<HashSet<GuildAudience>> GetEnabledAudiencesAsync(ulong guildId, GuildFeature feature) =>
        Snapshot is { } s ? Task.FromResult(s.GetEnabledAudiences(feature))
            : FeatureService.GetEnabledAudiencesAsync(guildId, feature);

    public Task<HashSet<int>> GetEnabledAllianceIdsAsync(ulong guildId, GuildFeature feature) =>
        Snapshot is { } s ? Task.FromResult(s.GetEnabledAllianceIds(feature))
            : FeatureService.GetEnabledAllianceIdsAsync(guildId, feature);

    public async Task<bool> HasAlertChannelAsync(ulong guildId, GuildAlertChannelKind kind, GuildAudience audience)
    {
        if (Snapshot is { } s)
            return s.HasAlertChannel(kind, audience);
        await using var db = await DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == kind && c.Audience == audience);
    }

    public async Task<bool> HasFeatureChannelAsync(ulong guildId, GuildFeature feature, GuildAudience audience)
    {
        if (Snapshot is { } s)
            return s.HasFeatureChannel(feature, audience);
        await using var db = await DbFactory.CreateDbContextAsync();
        return await db.GuildFeatureChannels.AnyAsync(c => c.GuildId == guildId && c.Feature == feature && c.Audience == audience);
    }
}

// One resolved dependency of a feature: the feature declaring it (OwnerFeature, needed to look up
// the note text — see Msg.WebGuild.DependencyNote), the required feature's module, whether that
// pairing carries a note, and whether the dependency is currently Enabled/Configured for the
// guild. Satisfied is the single signal the UI keys off — a feature with an unsatisfied dependency
// is treated as "not fully configured".
public record FeatureDependencyState(GuildFeature OwnerFeature, IFeatureModule Module, bool HasNote, bool Enabled, bool Configured)
{
    public bool Satisfied => Enabled && Configured;
}

public static class FeatureModuleExtensions
{
    public static GuildAudience RelevantAudiences(this IFeatureModule module) =>
        GuildFeatureAudiences.RelevantAudiences(module.Feature);

    public static bool HasMultipleAudiences(this IFeatureModule module) =>
        GuildFeatureAudiences.HasMultipleAudiences(module.Feature);

    // The features this one needs to actually work (from the Domain single source of truth),
    // resolved to their modules. Skips any that have no IFeatureModule (storage-only
    // pseudo-features) — none of the declared dependencies are such today, but stay defensive.
    public static IReadOnlyList<(IFeatureModule Module, bool HasNote)> Dependencies(this IFeatureModule module) =>
        GuildFeatureDependencies.Of(module.Feature)
            .Select(dep => (Module: FeatureCatalog.FindByFeature(dep.Feature), dep.HasNote))
            .Where(x => x.Module is not null)
            .Select(x => (x.Module!, x.HasNote))
            .ToList();

    // Resolves each dependency's live Enabled/Configured state for guildId. The dependent's own
    // (audience, guildAllianceId) is passed in; where the dependency is offered at that same
    // audience it's checked at that exact scope, otherwise at the dependency's own single audience
    // (guild-scoped, or same-alliance), otherwise — nothing to pin — via the "enabled under any
    // audience" guild-wide shim.
    public static async Task<IReadOnlyList<FeatureDependencyState>> GetDependencyStatesAsync(
        this IFeatureModule module, ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        var deps = GuildFeatureDependencies.Of(module.Feature);
        if (deps.Count == 0)
            return [];

        var states = new List<FeatureDependencyState>(deps.Count);
        foreach (var dep in deps)
        {
            var depModule = FeatureCatalog.FindByFeature(dep.Feature);
            if (depModule is null)
                continue;

            GuildAudience? checkAudience = null;
            int? checkScope = null;
            if (depModule.RelevantAudiences().HasFlag(audience))
            {
                checkAudience = audience;
                checkScope = guildAllianceId;
            }
            else if (!depModule.HasMultipleAudiences())
            {
                var single = GuildFeatureAudiences.SingleAudience(dep.Feature);
                var scope = single == GuildAudience.Alliance ? guildAllianceId : null;
                // Skip pinning an Alliance dependency when there's no alliance in context — falls
                // through to the guild-wide shim below rather than tripping FeatureScopeGuard.
                if (single != GuildAudience.Alliance || scope is not null)
                {
                    checkAudience = single;
                    checkScope = scope;
                }
            }

            bool enabled, configured;
            if (checkAudience is { } ca)
            {
                enabled = await context.IsEnabledAsync(guildId, dep.Feature, ca, checkScope);
                configured = enabled && await depModule.IsConfiguredAsync(guildId, ca, checkScope, context);
            }
            else
            {
                // Nothing to pin — the dependent's own audience doesn't overlap any audience the
                // dependency supports (e.g. a Guild-audience feature depending on an Alliance/Server/
                // VeilGroup/Community one like AiChat). Still check IsConfiguredAsync rather than
                // assuming "enabled anywhere" means configured: try each audience the dependency is
                // actually enabled under and treat it as configured if any one of them is — correct
                // for a guild-wide-settings dependency (its check ignores the audience argument
                // entirely, e.g. AiChatFeature's API key) and still meaningful for a genuinely
                // per-audience one.
                var enabledAudiences = await context.GetEnabledAudiencesAsync(guildId, dep.Feature);
                enabled = enabledAudiences.Count > 0;
                configured = false;
                foreach (var a in enabledAudiences)
                {
                    if (a == GuildAudience.Alliance)
                    {
                        // There's no alliance in the dependent's own context (it isn't Alliance-scoped
                        // itself), so check every alliance the dependency is actually enabled for —
                        // unlike the precise-pin case above, we can't assume "the same alliance" because
                        // there isn't one. GetEnabledAllianceIdsAsync only ever returns non-null ids, so
                        // this never trips FeatureScopeGuard.
                        foreach (var allianceId in await context.GetEnabledAllianceIdsAsync(guildId, dep.Feature))
                        {
                            if (await depModule.IsConfiguredAsync(guildId, GuildAudience.Alliance, allianceId, context))
                            {
                                configured = true;
                                break;
                            }
                        }
                    }
                    else if (await depModule.IsConfiguredAsync(guildId, a, null, context))
                    {
                        configured = true;
                    }

                    if (configured)
                        break;
                }
            }

            states.Add(new FeatureDependencyState(module.Feature, depModule, dep.HasNote, enabled, configured));
        }

        return states;
    }
}
