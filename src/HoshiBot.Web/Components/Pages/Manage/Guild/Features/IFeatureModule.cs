using HoshiBot.Data;
using HoshiBot.Domain.Entities;
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
    string Title { get; }
    string Description { get; }
    string Icon { get; }              // Open Iconic class, e.g. "oi-calendar"
    Type EditorComponentType { get; }

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
        context.FeatureService.IsEnabledAsync(guildId, Feature, audience, guildAllianceId);

    // Whether this feature's required settings are actually present for guildId+audience+alliance
    // — the Features page's yellow "enabled but not configured" state. Unlike IsEnabledAsync,
    // this genuinely varies per feature (different settings shapes), so every module
    // implements its own — no default body.
    Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context);
}

// One of a feature's auxiliary admin pages — see IFeatureModule.ExtraPages. ComponentType must be a
// component accepting a single [Parameter] ulong GuildId (FeatureExtraPage.razor is all that ever
// instantiates it, and only ever passes that one parameter).
public record FeatureExtraPage(string Slug, string Title, Type ComponentType);

// Everything a module's own checks might need, bundled so the interface members stay one
// parameter: FeatureService for IsEnabledAsync (uniform); Settings for most
// IsConfiguredAsync implementations; DbFactory for the 4 alert-list features' checks.
public record FeatureModuleContext(
    GuildFeatureService FeatureService,
    GuildFeatureSettingsService Settings,
    IDbContextFactory<HoshiBotDbContext> DbFactory);

// One resolved dependency of a feature: the required feature's module, its optional Note, and
// whether it's currently Enabled/Configured for the guild. Satisfied is the single signal the UI
// keys off — a feature with an unsatisfied dependency is treated as "not fully configured".
public record FeatureDependencyState(IFeatureModule Module, string? Note, bool Enabled, bool Configured)
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
    public static IReadOnlyList<(IFeatureModule Module, string? Note)> Dependencies(this IFeatureModule module) =>
        GuildFeatureDependencies.Of(module.Feature)
            .Select(dep => (Module: FeatureCatalog.FindByFeature(dep.Feature), dep.Note))
            .Where(x => x.Module is not null)
            .Select(x => (x.Module!, x.Note))
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
                enabled = await context.FeatureService.IsEnabledAsync(guildId, dep.Feature, ca, checkScope);
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
                var enabledAudiences = await context.FeatureService.GetEnabledAudiencesAsync(guildId, dep.Feature);
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
                        foreach (var allianceId in await context.FeatureService.GetEnabledAllianceIdsAsync(guildId, dep.Feature))
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

            states.Add(new FeatureDependencyState(depModule, dep.Note, enabled, configured));
        }

        return states;
    }
}
