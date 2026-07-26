using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Renames members' Discord nicknames to match the StfcPlayer that represents them in this guild
// (GuildMember.PrimaryStfcPlayerId, via PlayerLinkService.GetGuildPrimaryPlayersAsync), optionally
// prefixed with a server and/or alliance tag (NicknameSyncSettingKeys.{Server,Alliance}TagMode) so
// foreign players can be disambiguated — see docs/backlog.md "conditional nickname tagging". Runs for
// guilds with the (guild-wide) NicknameSync feature enabled. Members holding an excluded role are
// left alone entirely.
public class NicknameSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<NicknameSyncJob> logger) : IJob
{
    private const int DiscordNicknameMaxLength = 32;

    public Task Execute(IJobExecutionContext context) =>
        // Guild-wide, guild-scoped (null): only act when enabled for the Guild audience, ignoring
        // any orphaned rows left under other audiences.
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.NicknameSync, GuildAudience.Guild, logger, ProcessGuildAsync);

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

        // Whichever player represents each member *in this guild* — their own pick when they made
        // one, else their oldest link. The server label is built in memory below to avoid a
        // string+int concat in SQL.
        var members = (await playerLinkService.GetGuildPrimaryPlayersAsync(guildId)).Values;

        var roster = await GuildRoster.FetchAsync(gatewayClient, guildId);
        foreach (var member in members)
        {
            if (!roster.TryGetValue(member.DiscordUserId, out var guildUser))
                continue;
            var nickname = BuildNickname(member, allianceTagMode, serverTagMode, homeAllianceSet, homeServerSet);
            await SyncNicknameAsync(guildId, guildUser, nickname, excludedRoles);
        }
    }

    private static string BuildNickname(GuildPrimaryPlayer m, NicknameTagMode allianceMode, NicknameTagMode serverMode, HashSet<int> homeAlliances, HashSet<int> homeServers)
    {
        var serverLabel = $"{m.RegionName}{m.ServerId}";
        var serverTag = Include(serverMode, m.ServerId, homeServers) && !string.IsNullOrWhiteSpace(m.RegionName) ? $"[{serverLabel}]" : "";

        // A player with no alliance is treated as "foreign" (not one of the guild's own) and, when the
        // tag applies, shown as [n/a] so it's still disambiguated.
        var showAllianceTag = allianceMode switch
        {
            NicknameTagMode.Always => true,
            NicknameTagMode.ForeignOnly => m.AllianceId is not { } aid || !homeAlliances.Contains(aid),
            _ => false,
        };
        var allianceTag = showAllianceTag ? $"[{(string.IsNullOrWhiteSpace(m.AllianceTag) ? "n/a" : m.AllianceTag)}]" : "";

        var prefix = serverTag + allianceTag;
        var nickname = prefix.Length > 0 ? $"{prefix} {m.Name}" : m.Name;
        return nickname.Length > DiscordNicknameMaxLength ? nickname[..DiscordNicknameMaxLength] : nickname;
    }

    // Whether the server tag applies for this id under the given mode. ForeignOnly = the id is NOT one
    // of the guild's own servers.
    private static bool Include(NicknameTagMode mode, int id, HashSet<int> homeIds) => mode switch
    {
        NicknameTagMode.Always => true,
        NicknameTagMode.ForeignOnly => !homeIds.Contains(id),
        _ => false,
    };

    private static NicknameTagMode ParseMode(string? value) =>
        Enum.TryParse<NicknameTagMode>(value, out var mode) ? mode : NicknameTagMode.ForeignOnly;

    private async Task SyncNicknameAsync(ulong guildId, GuildUser guildUser, string targetNickname, HashSet<ulong> excludedRoles)
    {
        if (excludedRoles.Count > 0 && guildUser.RoleIds.Any(excludedRoles.Contains))
            return;
        if (guildUser.Nickname == targetNickname)
            return;

        try
        {
            await gatewayClient.Rest.ModifyGuildUserAsync(guildId, guildUser.Id, options => options.Nickname = targetNickname);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden)
        {
            // Bot's top role is below the member's — Discord won't allow renaming them.
            logger.LogInformation(
                "Skipped nickname sync for user {UserId} in guild {GuildId}: insufficient permission (role hierarchy)",
                guildUser.Id, guildId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            // Member left the guild since we last synced.
        }
    }
}
