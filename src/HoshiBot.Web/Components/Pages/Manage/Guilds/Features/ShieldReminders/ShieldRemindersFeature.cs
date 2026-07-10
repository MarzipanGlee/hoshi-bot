using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guilds.Features.ShieldReminders;

public class ShieldRemindersFeature : IFeatureModule
{
    public GuildFeature Feature => GuildFeature.ShieldReminders;
    public string Slug => "shield-reminders";
    public string Title => "Shield Reminders";

    public string Description =>
        "Members can set a reminder for when their shield expires; the bot pings them as it nears expiration and " +
        "posts a public alert if it actually expires unrenewed.";

    public string Icon => "oi-shield";
    public Type EditorComponentType => typeof(ShieldRemindersEditor);

    public async Task<bool> IsConfiguredAsync(ulong guildId, GuildAudience audience, FeatureModuleContext context)
    {
        if (await context.Settings.GetSnowflakeAsync(guildId, Feature, audience, "Channel") is not null)
            return true;

        await using var db = await context.DbFactory.CreateDbContextAsync();
        return await db.GuildAlertChannels.AnyAsync(c => c.GuildId == guildId && c.Kind == GuildAlertChannelKind.Shield && c.Audience == audience);
    }
}
