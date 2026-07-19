using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.MemberOnboarding;

public class MemberOnboardingFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.MemberOnboarding;
    public string Slug => "member-onboarding";
    public string Title => "Member Onboarding";

    public string Description =>
        "Opt-in: DMs members that Player Assignment couldn't place automatically, asking them to confirm the " +
        "bot's best guess or type their in-game player. Off by default — leave it off to resolve missing " +
        "assignments only via Player Assignment's admin table, without ever DMing members.";

    public string Icon => "oi-envelope-closed";
    public Type EditorComponentType => typeof(MemberOnboardingEditor);

    // Onboarding builds on Player Assignment's matcher, so it's "configured" once a member role is
    // resolvable — the PlayerLink setting or the linked alliance's GuildAlliance.MemberRoleId.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        if (await context.Settings.GetSnowflakeAsync(guildId, GuildFeature.PlayerLink, audience, guildAllianceId, PlayerLinkSettingKeys.MemberRole) is not null)
            return true;

        if (guildAllianceId is not { } gaId)
            return false;

        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlliances.AnyAsync(ga => ga.Id == gaId && ga.MemberRoleId != null);
    }
}
