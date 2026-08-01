using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.TerritoryCaptureServiceReminders;

public class TerritoryCaptureServiceRemindersFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.TerritoryCaptureServiceReminders;
    public string Slug => "territory-capture-service-reminders";

    public string Icon => "oi-wrench";
    public Type EditorComponentType => typeof(TerritoryCaptureServiceRemindersEditor);

    public IReadOnlyList<FeatureExtraPage> ExtraPages =>
        [new FeatureExtraPage("service-selection", typeof(ServiceSelectionAdmin))];

    // The channel is what makes the reminder actually fire (the job skips an alliance without one),
    // so that's the configured bar. The role is optional — no role just means no ping.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId,
            TerritoryCaptureServiceRemindersSettingKeys.ServicesChannel) is not null;
}
