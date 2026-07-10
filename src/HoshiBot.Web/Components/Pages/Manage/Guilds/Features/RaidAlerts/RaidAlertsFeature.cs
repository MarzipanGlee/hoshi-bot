using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.RaidAlerts;

public class RaidAlertsFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.RaidAlerts;
    public string Slug => "raid-alerts";
    public string Title => "Raid Alerts";

    public string Description =>
        "Members can report an incoming raid via /raid, posting a public alert to whichever channel(s) are " +
        "configured below.";

    public string Icon => "oi-bolt";
    public Type EditorComponentType => typeof(RaidAlertsEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context)
    {
        if (await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, "Channel") is not null)
            return true;

        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.Raid && c.Audience == audience);
    }
}
