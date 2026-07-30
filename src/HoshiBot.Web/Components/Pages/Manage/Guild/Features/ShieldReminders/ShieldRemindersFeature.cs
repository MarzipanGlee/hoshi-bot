using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ShieldReminders;

public class ShieldRemindersFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ShieldReminders;
    public string Slug => "shield-reminders";

    public string Icon => "oi-shield";
    public Type EditorComponentType => typeof(ShieldRemindersEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, FeatureModuleContext context)
    {
        if (await context.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, "Channel") is not null)
            return true;

        return await context.HasAlertChannelAsync(guildId, GuildAlertChannelKind.Shield, audience);
    }
}
