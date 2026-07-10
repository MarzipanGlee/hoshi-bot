using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.Incursion;

public class IncursionFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.Incursion;
    public string Slug => "incursion";
    public string Title => "Incursion Announcements";

    public string Description =>
        "Advance-warning announcement when a new Incursion event is scheduled.";

    public string Icon => "oi-warning";
    public Type EditorComponentType => typeof(IncursionEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.Incursion && c.Audience == audience);
    }
}
