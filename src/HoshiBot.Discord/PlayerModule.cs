using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

public class PlayerModule(HoshiBotDbContext db, PlayerLinkService playerLinkService, EmbedBranding embedBranding) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("link-player", "Link your Discord account to your STFC in-game player name",
        Contexts = [InteractionContextType.Guild])]
    public Task LinkPlayer(string playerName, string serverName) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var userId = Context.User.Id;

            var server = await db.StfcServers.FirstOrDefaultAsync(s => s.Name == serverName);
            if (server is null)
                return $"No server named \"{serverName}\" found. Ask an admin to add it first (via the web admin).";

            var player = await db.StfcPlayers.FirstOrDefaultAsync(p => p.ServerId == server.Id && p.Name == playerName);
            if (player is null)
            {
                player = new StfcPlayer { Name = playerName, ServerId = server.Id };
                db.StfcPlayers.Add(player);
                await db.SaveChangesAsync();
            }

            // The DiscordUser + UserPlayer core is shared with the automated PlayerLink matcher and
            // the Web admin table — see PlayerLinkService.LinkAsync, which also adopts this player as
            // the primary in any guild that has none yet. Record guild membership first so the
            // role-sync jobs (which iterate GuildMembers) apply roles.
            if (Context.Guild is { } guild)
                await playerLinkService.EnsureGuildMemberAsync(guild.Id, userId);
            await playerLinkService.LinkAsync(userId, player.Id);

            return $"Linked your Discord account to **{playerName}** on {server.Name}.";
        });

    [SlashCommand("set-my-alliance", "Set the alliance for the player representing you in this server",
        Contexts = [InteractionContextType.Guild])]
    public Task SetMyAlliance(string allianceTag) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var userId = Context.User.Id;

            var playerId = await playerLinkService.GetGuildPrimaryPlayerIdAsync(Context.Guild!.Id, userId);
            var player = playerId is null ? null : await db.StfcPlayers.FindAsync(playerId);
            if (player is null)
                return "You haven't linked a player yet. Use /link-player first.";

            var alliance = await db.StfcAlliances.FirstOrDefaultAsync(a =>
                a.ServerId == player.ServerId && a.Tag == allianceTag);
            if (alliance is null)
                return $"No alliance with tag \"{allianceTag}\" found on your server. Ask an admin to add it first (via the web admin).";

            player.AllianceId = alliance.Id;
            await db.SaveChangesAsync();

            return $"Set your alliance to {alliance.Name} ({alliance.Tag}).";
        });
}
