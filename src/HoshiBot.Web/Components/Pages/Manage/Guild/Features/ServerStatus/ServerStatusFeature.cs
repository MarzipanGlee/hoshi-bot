using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ServerStatus;

public class ServerStatusFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ServerStatus;
    public string Slug => "server-status";
    public string Title => "Server Status";

    public string Description =>
        "Announces when a tracked STFC server goes up/down or in/out of maintenance — relevant to any guild " +
        "tracking a server, not just alliances.";

    public string Icon => "oi-pulse";
    public Type EditorComponentType => typeof(ServerStatusEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.ServerStatus && c.Audience == audience);
    }
}
