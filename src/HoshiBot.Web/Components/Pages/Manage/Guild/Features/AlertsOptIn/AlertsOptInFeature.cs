using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AlertsOptIn;

public class AlertsOptInFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AlertsOptIn;
    public string Slug => "alerts-opt-in";

    public string Icon => "oi-bell";
    public Type EditorComponentType => typeof(AlertsOptInEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, AlertsOptInSettingKeys.Role) is not null;
}
