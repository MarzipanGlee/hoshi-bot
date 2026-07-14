using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.InfiniteIncursions;

public class InfiniteIncursionsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.InfiniteIncursions;
    public string Slug => "infinite-incursions";
    public string Title => "Infinite Incursions Announcements";

    public string Description =>
        "Advance-warning announcement when a new Infinite Incursions event is scheduled.";

    public string Icon => "oi-warning";
    public Type EditorComponentType => typeof(InfiniteIncursionsEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.InfiniteIncursions && c.Audience == audience);
    }
}
