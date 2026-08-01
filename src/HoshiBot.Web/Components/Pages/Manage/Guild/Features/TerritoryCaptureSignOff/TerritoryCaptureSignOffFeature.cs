using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.TerritoryCaptureSignOff;

public class TerritoryCaptureSignOffFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.TerritoryCaptureSignOff;
    public string Slug => "territory-capture-sign-off";

    public string Icon => "oi-account-logout";
    public Type EditorComponentType => typeof(TerritoryCaptureSignOffEditor);

    // Nothing to configure — the feature is the toggle. What it actually needs (Territory Capture
    // and Absences enabled) is declared in GuildFeatureDependencies, and the Features page already
    // drops a feature to the yellow warning state when a dependency is unmet, so returning true
    // here doesn't paint an unusable setup green.
    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        Task.FromResult(true);
}
