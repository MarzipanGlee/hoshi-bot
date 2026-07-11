using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.OpsLevelRoles;

public class OpsLevelRolesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.OpsLevelRoles;
    public string Slug => "ops-level-roles";
    public string Title => "Ops Level Roles";

    public string Description =>
        "Assigns each member one of seven Discord roles matching their current STFC Ops Level " +
        "tier (G1-G7), kept in sync from imported player data — usable by any audience.";

    public string Icon => "oi-graph";
    public Type EditorComponentType => typeof(OpsLevelRolesEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context)
    {
        foreach (var key in new[]
        {
            OpsLevelRolesSettingKeys.G1Role, OpsLevelRolesSettingKeys.G2Role, OpsLevelRolesSettingKeys.G3Role,
            OpsLevelRolesSettingKeys.G4Role, OpsLevelRolesSettingKeys.G5Role, OpsLevelRolesSettingKeys.G6Role,
            OpsLevelRolesSettingKeys.G7Role,
        })
        {
            if (await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, key) is not null)
                return true;
        }

        return false;
    }
}
