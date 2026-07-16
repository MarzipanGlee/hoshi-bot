using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace HoshiBot.Host;

// Keeps the Guilds table in sync with the guilds Discord tells us about on
// gateway connect (and whenever the bot joins a new guild), so the web admin's
// guild picker has something to show without a separate sync step.
public class GuildSyncHandler(IServiceScopeFactory scopeFactory) : IGuildCreateGatewayHandler
{
    public async ValueTask HandleAsync(GuildCreateEventArgs eventArgs)
    {
        if (eventArgs.Guild is not { } guild)
            return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();

        var existing = await db.DiscordGuilds.FindAsync(guild.Id);
        if (existing is null)
        {
            db.DiscordGuilds.Add(new DiscordGuild { Id = guild.Id, Name = guild.Name, IconHash = guild.IconHash });
        }
        else
        {
            existing.Name = guild.Name;
            existing.IconHash = guild.IconHash;
        }

        await db.SaveChangesAsync();
    }
}
