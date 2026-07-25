using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.HoshiSay;

public class HoshiSayFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.HoshiSay;
    public string Slug => "hoshi-say";
    public string Title => "Hoshi Say";

    public string Description =>
        "Lets an admin have Hoshi compose a message in her own voice (via the AI backend) and post it as " +
        "a plain chat line into the current channel — e.g. to comfort or address a member. The /hoshi-say " +
        "command is limited to members holding the configured allowed role, which is the permission gate.";

    public string Icon => "oi-comment-square";
    public Type EditorComponentType => typeof(HoshiSayEditor);

    // Configured once the allowed role is set (the role permitted to run the command).
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, HoshiSaySettingKeys.AllowedRole) is not null;
}
