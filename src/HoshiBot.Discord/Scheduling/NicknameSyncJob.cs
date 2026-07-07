using System.Net;
using HoshiBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Renames members' Discord nicknames to match their main linked StfcPlayer's name, but
// only for guilds that opted in via DiscordGuild.NicknameSyncEnabled (/nickname-sync command).
public class NicknameSyncJob(HoshiBotDbContext db, GatewayClient gatewayClient, ILogger<NicknameSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var guildIds = await db.DiscordGuilds
            .Where(g => g.NicknameSyncEnabled)
            .Select(g => g.Id)
            .ToListAsync();

        foreach (var guildId in guildIds)
        {
            var members = await db.GuildMembers
                .Where(gm => gm.GuildId == guildId)
                .Select(gm => new
                {
                    gm.DiscordUserId,
                    PlayerName = gm.User.PlayerLinks
                        .Where(up => up.IsMain)
                        .Select(up => up.StfcPlayer.Name)
                        .FirstOrDefault(),
                })
                .Where(m => m.PlayerName != null)
                .ToListAsync();

            foreach (var member in members)
            {
                await SyncNicknameAsync(guildId, member.DiscordUserId, member.PlayerName!);
            }
        }
    }

    private async Task SyncNicknameAsync(ulong guildId, ulong userId, string inGameName)
    {
        try
        {
            var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
            if (guildUser.Nickname == inGameName)
                return;

            await gatewayClient.Rest.ModifyGuildUserAsync(guildId, userId, options => options.Nickname = inGameName);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden)
        {
            // Bot's top role is below the member's — Discord won't allow renaming them.
            logger.LogInformation(
                "Skipped nickname sync for user {UserId} in guild {GuildId}: insufficient permission (role hierarchy)",
                userId, guildId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            // Member left the guild since we last synced.
        }
    }
}
