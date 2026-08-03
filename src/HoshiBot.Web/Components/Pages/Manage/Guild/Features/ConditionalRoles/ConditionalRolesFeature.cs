using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ConditionalRoles;

public class ConditionalRolesFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ConditionalRoles;
    public string Slug => "conditional-roles";

    public string Icon => "oi-fork";
    public Type EditorComponentType => typeof(ConditionalRolesEditor);

    // The rules and the reusable conditions each need more room than a settings card, so the card
    // only links to them.
    public IReadOnlyList<FeatureExtraPage> ExtraPages =>
        [
            new FeatureExtraPage("rules", typeof(RulesAdmin)),
            new FeatureExtraPage("conditions", typeof(ConditionsAdmin)),
        ];

    // Configured once there's an enabled rule with a target role. A rule whose tree is unfinished
    // still counts here — the editor flags that case itself, and the sync's fail-closed behaviour
    // means it simply grants nothing meanwhile.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.ConditionalRoleRules.AnyAsync(r => r.GuildId == guildId && r.Enabled && r.TargetRoleId != 0);
    }
}
