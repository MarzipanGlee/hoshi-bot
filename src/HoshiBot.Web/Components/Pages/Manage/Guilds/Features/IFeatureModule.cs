using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features;

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

    // Identical logic for every feature (just checks GuildEnabledFeature for Feature+
    // audience) — a default interface method, so this is written once here rather than
    // duplicated verbatim across all 12 module classes.
    Task<bool> IsEnabledAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context) =>
        context.FeatureService.IsEnabledAsync(guildId, Feature, audience);

    // Whether this feature's required settings are actually present for guildId+audience —
    // the Features page's yellow "enabled but not configured" state. Unlike IsEnabledAsync,
    // this genuinely varies per feature (different settings shapes), so every module
    // implements its own — no default body.
    Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context);
}

// Everything a module's own checks might need, bundled so the interface members stay one
// parameter: FeatureService for IsEnabledAsync (uniform); Settings for most
// IsConfiguredAsync implementations; DbFactory for the 4 alert-list features' checks.
public record FeatureModuleContext(
    GuildFeatureService FeatureService,
    GuildFeatureSettingsService Settings,
    IDbContextFactory<HoshiBotDbContext> DbFactory);

public static class FeatureModuleExtensions
{
    public static GuildAudience RelevantAudiences(this IFeatureModule module) =>
        GuildFeatureAudiences.RelevantAudiences(module.Feature);

    public static bool HasMultipleAudiences(this IFeatureModule module) =>
        GuildFeatureAudiences.HasMultipleAudiences(module.Feature);
}
