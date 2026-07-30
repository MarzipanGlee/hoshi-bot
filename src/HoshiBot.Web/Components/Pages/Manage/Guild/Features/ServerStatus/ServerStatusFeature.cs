using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ServerStatus;

public class ServerStatusFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ServerStatus;
    public string Slug => "server-status";

    public string Icon => "oi-pulse";
    public Type EditorComponentType => typeof(ServerStatusEditor);

    public Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context) =>
        context.HasAlertChannelAsync(guildId, GuildAlertChannelKind.ServerStatus, audience);
}
