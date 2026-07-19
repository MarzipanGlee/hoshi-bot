using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

public class PlayerModule(HoshiBotDbContext db, PlayerLinkService playerLinkService) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("link-player", "Link your Discord account to your STFC in-game player name",
        Contexts = [InteractionContextType.Guild])]
    public Task LinkPlayer(string playerName, string serverName) =>
        Context.Interaction.SendDelayedResponseAsync(async () =>
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

            // The DiscordUser + UserPlayer (IsMain-when-first) core is shared with the automated
            // PlayerLink matcher and the Web admin table — see PlayerLinkService.LinkAsync.
            await playerLinkService.LinkAsync(userId, player.Id);

            return $"Linked your Discord account to **{playerName}** on {server.Name}.";
        });

    [SlashCommand("set-my-alliance", "Set the alliance for your main linked player",
        Contexts = [InteractionContextType.Guild])]
    public Task SetMyAlliance(string allianceTag) =>
        Context.Interaction.SendDelayedResponseAsync(async () =>
        {
            var userId = Context.User.Id;

            var mainLink = await db.UserPlayers
                .Include(up => up.StfcPlayer)
                .FirstOrDefaultAsync(up => up.DiscordUserId == userId && up.IsMain);
            if (mainLink is null)
                return "You haven't linked a player yet. Use /link-player first.";

            var alliance = await db.StfcAlliances.FirstOrDefaultAsync(a =>
                a.ServerId == mainLink.StfcPlayer.ServerId && a.Tag == allianceTag);
            if (alliance is null)
                return $"No alliance with tag \"{allianceTag}\" found on your server. Ask an admin to add it first (via the web admin).";

            mainLink.StfcPlayer.AllianceId = alliance.Id;
            await db.SaveChangesAsync();

            return $"Set your alliance to {alliance.Name} ({alliance.Tag}).";
        });

    [SlashCommand("nickname-sync", "Enable or disable automatic Discord nickname sync for this server",
        DefaultGuildPermissions = Permissions.ManageGuild, Contexts = [InteractionContextType.Guild])]
    public Task NicknameSync(bool enabled) =>
        Context.Interaction.SendDelayedResponseAsync(async () =>
        {
            var guildId = Context.Guild!.Id;

            var guild = await db.DiscordGuilds.FindAsync(guildId)
                ?? throw new InvalidOperationException($"Guild {guildId} is not yet known to Hoshi Bot.");

            guild.NicknameSyncEnabled = enabled;
            await db.SaveChangesAsync();

            return enabled
                ? "Nickname sync enabled — members' Discord nicknames will be kept in sync with their linked in-game name."
                : "Nickname sync disabled.";
        });
}
