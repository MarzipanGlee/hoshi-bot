using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ServerTagRoles;

public class ServerTagRolesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ServerTagRoles;
    public string Slug => "server-tag-roles";

    public string Icon => "oi-globe";
    public Type EditorComponentType => typeof(ServerTagRolesEditor);

    // Configured once at least one role is picked — a server's own, or the foreign-server catch-all.
    // Unlike Alliance Tag Roles nothing is ever created by the sync itself, so with no role picked
    // there is genuinely nothing to assign: that's the yellow "enabled but not configured" state.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        if (await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, ServerTagRolesSettingKeys.ForeignServerRole) is not null)
            return true;

        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingSnowflakes.AnyAsync(s =>
            s.GuildId == guildId && s.Feature == Feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId
            && s.Key.StartsWith(ServerTagRolesSettingKeys.RolePrefixKey));
    }
}
