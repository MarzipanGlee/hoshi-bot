using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.OpsLevelRoles;

public class OpsLevelRolesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.OpsLevelRoles;
    public string Slug => "ops-level-roles";

    public string Icon => "oi-graph";
    public Type EditorComponentType => typeof(OpsLevelRolesEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        foreach (var key in new[]
        {
            OpsLevelRolesSettingKeys.G1Role, OpsLevelRolesSettingKeys.G2Role, OpsLevelRolesSettingKeys.G3Role,
            OpsLevelRolesSettingKeys.G4Role, OpsLevelRolesSettingKeys.G5Role, OpsLevelRolesSettingKeys.G6Role,
            OpsLevelRolesSettingKeys.G7Role,
        })
        {
            if (await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, key) is not null)
                return true;
        }

        return false;
    }
}
