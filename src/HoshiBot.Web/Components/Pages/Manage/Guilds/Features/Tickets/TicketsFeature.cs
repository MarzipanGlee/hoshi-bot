using HoshiBot.Data;
using HoshiBot.Domain.Entities;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Tickets;

public class TicketsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Tickets;
    public string Slug => "tickets";
    public string Title => "Tickets";

    public string Description =>
        "Members can open a private support thread with staff via the hub button — usable by any audience.";

    public string Icon => "oi-task";
    public Type EditorComponentType => typeof(TicketsEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context) =>
        await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, TicketsSettingKeys.Channel) is not null;
}
