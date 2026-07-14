using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.TerritoryCapture;

public class TerritoryCaptureFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.TerritoryCapture;
    public string Slug => "territory-capture";
    public string Title => "Territory Capture";

    public string Description =>
        "Zone-slot and rank role sync, plus the weekly/daily Territory Capture digest summarizing owned zones and " +
        "next capture times.";

    public string Icon => "oi-map";
    public Type EditorComponentType => typeof(TerritoryCaptureEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        for (var slot = 1; slot <= 5; slot++)
        {
            if (await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, TerritoryCaptureSettingKeys.ZoneSlotRole(slot)) is not null)
                return true;
        }

        return false;
    }
}
