using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.AllianceTagRoles;

public class AllianceTagRolesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.AllianceTagRoles;
    public string Slug => "alliance-tag-roles";

    public string Icon => "oi-tags";
    public Type EditorComponentType => typeof(AllianceTagRolesEditor);

    // Configured once it can actually do something: it may create the roles it needs, or it has
    // already bound at least one alliance to a role, or a no-alliance role is set. With
    // create-missing off and nothing bound yet, the sync has nothing to assign — that's the yellow
    // "enabled but not configured" state.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        var createMissing = await context.GetTextAsync(guildId, Feature, audience, guildAllianceId, AllianceTagRolesSettingKeys.CreateMissing);
        if (!string.Equals(createMissing, "false", StringComparison.OrdinalIgnoreCase))
            return true;

        if (await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, AllianceTagRolesSettingKeys.NoAllianceRole) is not null)
            return true;

        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingSnowflakes.AnyAsync(s =>
            s.GuildId == guildId && s.Feature == Feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId
            && s.Key.StartsWith(AllianceTagRolesSettingKeys.RolePrefixKey));
    }
}
