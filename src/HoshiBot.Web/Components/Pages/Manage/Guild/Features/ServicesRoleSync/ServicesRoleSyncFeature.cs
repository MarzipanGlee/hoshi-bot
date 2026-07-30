using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ServicesRoleSync;

public class ServicesRoleSyncFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ServicesRoleSync;
    public string Slug => "services-role-sync";

    public string Icon => "oi-loop-circular";
    public Type EditorComponentType => typeof(ServicesRoleSyncEditor);

    // Configured once the Services role it assigns exists — that role is owned by the Territory
    // Capture feature (this feature carries no settings of its own; it shares TC's ServicesRole).
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, GuildFeature.TerritoryCapture, audience, guildAllianceId, TerritoryCaptureSettingKeys.ServicesRole) is not null;
}
