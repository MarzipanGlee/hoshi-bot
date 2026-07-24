using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.RaidAlerts;

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

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        if (await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, "Channel") is not null)
            return true;

        return await context.HasAlertChannelAsync(guildId, GuildAlertChannelKind.Raid, audience);
    }
}
