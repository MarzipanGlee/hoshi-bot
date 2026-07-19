using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.RankRoles;

public class RankRolesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.RankRoles;
    public string Slug => "rank-roles";
    public string Title => "Rank Roles";

    public string Description =>
        "Assigns each member one of five Discord roles matching their current STFC in-alliance rank " +
        "(Admiral/Commodore/Premier/Operative/Agent), kept in sync from imported player data — one set " +
        "of roles for the whole guild.";

    public string Icon => "oi-badge";
    public Type EditorComponentType => typeof(RankRolesEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        foreach (var key in new[]
        {
            RankRolesSettingKeys.AdmiralRole, RankRolesSettingKeys.CommodoreRole, RankRolesSettingKeys.PremierRole,
            RankRolesSettingKeys.OperativeRole, RankRolesSettingKeys.AgentRole,
        })
        {
            if (await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, key) is not null)
                return true;
        }

        return false;
    }
}
