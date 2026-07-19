using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.PlayerLink;

public class PlayerLinkFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.PlayerLink;
    public string Slug => "player-link";
    public string Title => "Player Assignment";

    public string Description =>
        "Automatically links members to their STFC player by matching Discord nicknames against the " +
        "alliance roster — which drives rank/ops/nickname role sync. Members it can't resolve are listed " +
        "here for you to assign by hand. Never messages members.";

    public string Icon => "oi-link-intact";
    public Type EditorComponentType => typeof(PlayerLinkEditor);

    // Configured once a member role is resolvable — the explicit PlayerLink setting or the linked
    // alliance's own GuildAlliance.MemberRoleId (the fallback the matcher uses).
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        if (await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, PlayerLinkSettingKeys.MemberRole) is not null)
            return true;

        if (guildAllianceId is not { } gaId)
            return false;

        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlliances.AnyAsync(ga => ga.Id == gaId && ga.MemberRoleId != null);
    }
}
