using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.Tickets;

public class TicketsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Tickets;
    public string Slug => "tickets";

    public string Icon => "oi-task";
    public Type EditorComponentType => typeof(TicketsEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, TicketsSettingKeys.Channel) is not null;
}
