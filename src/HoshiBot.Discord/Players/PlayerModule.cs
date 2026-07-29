using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Players;

public class PlayerModule(HoshiBotDbContext db, PlayerLinkService playerLinkService, EmbedBranding embedBranding, LanguageResolver languageResolver) : ApplicationCommandModule<ApplicationCommandContext>
{
    // Both commands only ever reply ephemerally to the invoking member — their language.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    [SlashCommand("link-player", "Link your Discord account to your STFC in-game player name",
        Contexts = [InteractionContextType.Guild])]
    public Task LinkPlayer(string playerName, string serverName) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var userId = Context.User.Id;
            var lang = await ActingUserLanguageAsync();

            var server = await db.StfcServers.FirstOrDefaultAsync(s => s.Name == serverName);
            if (server is null)
                return Msg.Player.ServerNotFound(lang, serverName);

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

            return Msg.Player.Linked(lang, playerName, server.Name);
        });

    [SlashCommand("set-my-alliance", "Set the alliance for the player representing you in this server",
        Contexts = [InteractionContextType.Guild])]
    public Task SetMyAlliance(string allianceTag) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var userId = Context.User.Id;
            var lang = await ActingUserLanguageAsync();

            var playerId = await playerLinkService.GetGuildPrimaryPlayerIdAsync(Context.Guild!.Id, userId);
            var player = playerId is null ? null : await db.StfcPlayers.FindAsync(playerId);
            if (player is null)
                return Msg.Player.NoLinkedPlayer(lang);

            var alliance = await db.StfcAlliances.FirstOrDefaultAsync(a =>
                a.ServerId == player.ServerId && a.Tag == allianceTag);
            if (alliance is null)
                return Msg.Player.AllianceNotFound(lang, allianceTag);

            player.AllianceId = alliance.Id;
            await db.SaveChangesAsync();

            return Msg.Player.AllianceSet(lang, alliance.Name, alliance.Tag);
        });
}
