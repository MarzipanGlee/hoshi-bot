using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.StfcNews;

public class StfcNewsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.StfcNews;
    public string Slug => "stfc-news";

    public string Icon => "oi-rss";
    public Type EditorComponentType => typeof(StfcNewsEditor);

    // No feature-specific setting of its own — reuses the guild-wide AdminChannelId
    // (Global Settings), so "configured" just mirrors whether that's set.
    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        await using var db = await context.DbFactory.CreateDbContextAsync();
        var settings = await db.GuildSettings.AsNoTracking().FirstOrDefaultAsync(s => s.GuildId == guildId);
        return settings?.AdminChannelId is not null;
    }
}
