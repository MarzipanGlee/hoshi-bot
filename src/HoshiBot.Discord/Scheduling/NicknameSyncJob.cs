using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Renames members' Discord nicknames to match their main linked StfcPlayer's name, optionally
// prefixed with a server and/or alliance tag (NicknameSyncSettingKeys.{Server,Alliance}TagMode) so
// foreign players can be disambiguated — see docs/backlog.md "conditional nickname tagging". Runs for
// guilds with the (guild-wide) NicknameSync feature enabled. Members holding an excluded role are
// left alone entirely.
public class NicknameSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    ILogger<NicknameSyncJob> logger) : IJob
{
    private const int DiscordNicknameMaxLength = 32;

    public async Task Execute(IJobExecutionContext context)
    {
        var guildIds = await db.GuildEnabledFeatures
            .Where(f => f.Feature == GuildFeature.NicknameSync)
            .Select(f => f.GuildId)
            .Distinct()
            .ToListAsync();

        foreach (var guildId in guildIds)
        {
            // Guild-wide, guild-scoped (null): only act when enabled for the Guild audience, ignoring
            // any orphaned rows left under other audiences.
            if (!await featureService.IsEnabledAsync(guildId, GuildFeature.NicknameSync, GuildAudience.Guild, null))
                continue;

            try
            {
                await ProcessGuildAsync(guildId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nickname sync failed for guild {GuildId}", guildId);
            }
        }
    }

    private async Task ProcessGuildAsync(ulong guildId)
    {
        var allianceTagMode = ParseMode(await settingsService.GetTextAsync(guildId, GuildFeature.NicknameSync, GuildAudience.Guild, null, NicknameSyncSettingKeys.AllianceTagMode));
        var serverTagMode = ParseMode(await settingsService.GetTextAsync(guildId, GuildFeature.NicknameSync, GuildAudience.Guild, null, NicknameSyncSettingKeys.ServerTagMode));
        var excludedRoles = (await settingsService.GetSnowflakeListAsync(guildId, GuildFeature.NicknameSync, GuildAudience.Guild, null, NicknameSyncSettingKeys.ExcludedRoles)).ToHashSet();

        // Home = the guild's own alliances and their servers (plus any explicitly tracked servers).
        var homeAllianceIds = await db.GuildAlliances.Where(ga => ga.GuildId == guildId).Select(ga => ga.StfcAllianceId).ToListAsync();
        var homeAllianceSet = homeAllianceIds.ToHashSet();
        var homeServerSet = (await db.StfcAlliances.Where(a => homeAllianceIds.Contains(a.Id)).Select(a => a.ServerId).ToListAsync())
            .Concat(await db.GuildServers.Where(gs => gs.GuildId == guildId).Select(gs => gs.StfcServerId).ToListAsync())
            .ToHashSet();

        // Query from the main UserPlayer links of this guild's members — a flat, translatable shape
        // (the earlier per-field correlated subqueries into a constructor couldn't be translated). The
        // server label is built in memory below to avoid a string+int concat in SQL.
        var members = await db.UserPlayers
            .Where(up => up.IsMain && up.User.GuildMemberships.Any(gm => gm.GuildId == guildId))
            .Select(up => new MainPlayer(
                up.DiscordUserId,
                up.StfcPlayer.Name,
                up.StfcPlayer.AllianceId,
                up.StfcPlayer.Alliance != null ? up.StfcPlayer.Alliance.Tag : null,
                up.StfcPlayer.ServerId,
                up.StfcPlayer.Server.Region.Name))
            .ToListAsync();

        foreach (var member in members)
        {
            var nickname = BuildNickname(member, allianceTagMode, serverTagMode, homeAllianceSet, homeServerSet);
            await SyncNicknameAsync(guildId, member.DiscordUserId, nickname, excludedRoles);
        }
    }

    private static string BuildNickname(MainPlayer m, NicknameTagMode allianceMode, NicknameTagMode serverMode, HashSet<int> homeAlliances, HashSet<int> homeServers)
    {
        var serverLabel = $"{m.RegionName}{m.ServerId}";
        var serverTag = Include(serverMode, m.ServerId, homeServers) && !string.IsNullOrWhiteSpace(m.RegionName) ? $"[{serverLabel}]" : "";
        var allianceTag = m.AllianceId is { } aid && Include(allianceMode, aid, homeAlliances) && !string.IsNullOrWhiteSpace(m.AllianceTag) ? $"[{m.AllianceTag}]" : "";

        var prefix = serverTag + allianceTag;
        var nickname = prefix.Length > 0 ? $"{prefix} {m.PlayerName}" : m.PlayerName!;
        return nickname.Length > DiscordNicknameMaxLength ? nickname[..DiscordNicknameMaxLength] : nickname;
    }

    // Whether a tag applies for this id under the given mode. ForeignOnly = the id is NOT one of the
    // guild's own (alliance/server).
    private static bool Include(NicknameTagMode mode, int id, HashSet<int> homeIds) => mode switch
    {
        NicknameTagMode.Always => true,
        NicknameTagMode.ForeignOnly => !homeIds.Contains(id),
        _ => false,
    };

    private static NicknameTagMode ParseMode(string? value) =>
        Enum.TryParse<NicknameTagMode>(value, out var mode) ? mode : NicknameTagMode.ForeignOnly;

    private async Task SyncNicknameAsync(ulong guildId, ulong userId, string targetNickname, HashSet<ulong> excludedRoles)
    {
        try
        {
            var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
            if (excludedRoles.Count > 0 && guildUser.RoleIds.Any(excludedRoles.Contains))
                return;
            if (guildUser.Nickname == targetNickname)
                return;

            await gatewayClient.Rest.ModifyGuildUserAsync(guildId, userId, options => options.Nickname = targetNickname);
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

    private record MainPlayer(ulong DiscordUserId, string PlayerName, int? AllianceId, string? AllianceTag, int ServerId, string? RegionName);
}
