using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ChannelGuide;

public class ChannelGuideFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ChannelGuide;
    public string Slug => "channel-guide";

    public string Icon => "oi-signpost";
    public Type EditorComponentType => typeof(ChannelGuideEditor);

    // The message IS the feature — enabled with nothing written shows a placeholder on the bridge,
    // so an empty text is genuinely unconfigured rather than a valid default.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        !string.IsNullOrWhiteSpace(await context.GetTextAsync(guildId, Feature, audience, guildAllianceId, ChannelGuideSettingKeys.Message));
}
