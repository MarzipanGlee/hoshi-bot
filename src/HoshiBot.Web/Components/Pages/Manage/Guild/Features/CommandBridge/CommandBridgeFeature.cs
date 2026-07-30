using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.CommandBridge;

public class CommandBridgeFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.CommandBridge;
    public string Slug => "command-bridge";

    public string Icon => "oi-grid-three-up";
    public Type EditorComponentType => typeof(CommandBridgeEditor);

    // Configured once this alliance has at least one bridge channel set (the channels/message-ids
    // are typed columns on GuildAlliance, not the generic settings store).
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        if (guildAllianceId is not { } id)
            return false;

        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlliances
            .Where(a => a.Id == id && a.GuildId == guildId)
            .AnyAsync(a => a.CommandBridgeChannelId != null
                || a.StaffCommandBridgeChannelId != null
                || a.FriendsCommandBridgeChannelId != null);
    }
}
