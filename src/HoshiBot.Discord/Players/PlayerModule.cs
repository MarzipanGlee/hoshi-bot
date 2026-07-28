using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Players;

public class PlayerModule(HoshiBotDbContext db, PlayerLinkService playerLinkService, EmbedBranding embedBranding) : ApplicationCommandModule<ApplicationCommandContext>
{
    // All strings come from the message catalog (Msg.Player); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    [SlashCommand("link-player", "Link your Discord account to your STFC in-game player name",
        Contexts = [InteractionContextType.Guild])]
    public Task LinkPlayer(string playerName, string serverName) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var userId = Context.User.Id;

            var server = await db.StfcServers.FirstOrDefaultAsync(s => s.Name == serverName);
            if (server is null)
                return Msg.Player.ServerNotFound(Lang, serverName);

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

            return Msg.Player.Linked(Lang, playerName, server.Name);
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
                return Msg.Player.NoLinkedPlayer(Lang);

            var alliance = await db.StfcAlliances.FirstOrDefaultAsync(a =>
                a.ServerId == player.ServerId && a.Tag == allianceTag);
            if (alliance is null)
                return Msg.Player.AllianceNotFound(Lang, allianceTag);

            player.AllianceId = alliance.Id;
            await db.SaveChangesAsync();

            return Msg.Player.AllianceSet(Lang, alliance.Name, alliance.Tag);
        });
}
