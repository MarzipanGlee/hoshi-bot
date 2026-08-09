using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.Boarding;

public class BoardingFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Boarding;
    public string Slug => "boarding";

    public string Icon => "oi-key";
    public Type EditorComponentType => typeof(BoardingEditor);

    // Everything the button needs to work: somewhere to post, something to say, and both roles. The
    // service refuses to publish without all four, so the Features page should say so too rather
    // than showing a green feature whose message never appears.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        if (await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, BoardingSettingKeys.Channel) is null)
            return false;

        if (string.IsNullOrWhiteSpace(await context.GetTextAsync(guildId, Feature, audience, guildAllianceId, BoardingSettingKeys.Message)))
            return false;

        var member = await context.GetScopeRoleAsync(guildId, audience, guildAllianceId, a => a.MemberRoleId, a => a.MemberRoleId);
        var boarding = await context.GetScopeRoleAsync(guildId, audience, guildAllianceId, a => a.BoardingRoleId, a => a.BoardingRoleId);
        return member is not null && boarding is not null;
    }
}
