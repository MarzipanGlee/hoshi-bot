using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.BotSupport;

public class BotSupportFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.BotSupport;
    public string Slug => "bot-support";

    public string Icon => "oi-question-mark";
    public Type EditorComponentType => typeof(BotSupportEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, BotSupportSettingKeys.Channel) is not null;
}
