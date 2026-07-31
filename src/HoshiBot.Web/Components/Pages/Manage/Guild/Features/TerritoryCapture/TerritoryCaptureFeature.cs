using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.TerritoryCapture;

public class TerritoryCaptureFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.TerritoryCapture;
    public string Slug => "territory-capture";

    public string Icon => "oi-map";
    public Type EditorComponentType => typeof(TerritoryCaptureEditor);

    public IReadOnlyList<FeatureExtraPage> ExtraPages =>
        [new FeatureExtraPage("service-selection", typeof(ServiceSelectionAdmin))];

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        // Absence sign-off (default-on) puts "Abmelden" buttons on the digests/reminders, and those
        // write Absence rows — so with the switch on but the Absences feature off, TC is enabled but
        // not workable. Not a GuildFeatureDependencies entry: that table is unconditional, while this
        // dependency only exists while the switch is on.
        var signOff = TerritoryCaptureSettingKeys.IsAbsenceSignOffOn(
            await context.GetTextAsync(guildId, Feature, audience, guildAllianceId, TerritoryCaptureSettingKeys.AbsenceSignOff));
        if (signOff && !await context.IsEnabledAsync(guildId, GuildFeature.Absences, audience, guildAllianceId))
            return false;

        for (var slot = 1; slot <= 5; slot++)
        {
            if (await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, TerritoryCaptureSettingKeys.ZoneSlotRole(slot)) is not null)
                return true;
        }

        return false;
    }
}
