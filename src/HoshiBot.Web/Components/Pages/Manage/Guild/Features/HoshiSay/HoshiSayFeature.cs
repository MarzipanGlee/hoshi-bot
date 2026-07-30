using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.HoshiSay;

public class HoshiSayFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.HoshiSay;
    public string Slug => "hoshi-say";

    public string Icon => "oi-comment-square";
    public Type EditorComponentType => typeof(HoshiSayEditor);

    // Configured once the allowed role is set (the role permitted to run the command).
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, HoshiSaySettingKeys.AllowedRole) is not null;
}
