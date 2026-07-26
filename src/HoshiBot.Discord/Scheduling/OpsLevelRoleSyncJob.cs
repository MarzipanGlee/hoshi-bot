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

// Keeps each guild's configured Ops Level roles (OpsLevelRolesSettingKeys.{G1..G7}Role) in
// sync with each member's current STFC Ops Level (StfcPlayer.OpsLevel, set by a player-data
// import — see StfcPlayerImportService), bucketed into a G1-G7 tier via
// StfcOpsGroupExtensions.FromLevel: the one role matching their tier is added, the other 6
// are removed if present, so leveling up/down swaps the role instead of accumulating old
// ones. OpsLevelRoles is a single guild-wide feature (GuildAudience.Guild) — one set of 7
// roles for the whole guild, not per-audience or per-alliance.
public class OpsLevelRoleSyncJob(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<OpsLevelRoleSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var guildIds = await featureService.GetEnabledGuildIdsAsync(GuildFeature.OpsLevelRoles);

        foreach (var guildId in guildIds)
        {
            // Guild-wide, guild-scoped (null): only act when enabled for the Guild audience,
            // ignoring any orphaned rows left under other audiences by the audience refactor.
            if (!await featureService.IsEnabledAsync(guildId, GuildFeature.OpsLevelRoles, GuildAudience.Guild, null))
                continue;

            // Ops level comes from whichever player represents the member in *this* guild.
            var members = (await playerLinkService.GetGuildPrimaryPlayersAsync(guildId)).Values
                .Select(p => new MemberOpsLevel(p.DiscordUserId, p.OpsLevel))
                .ToList();

            var roster = await GuildRoster.FetchAsync(gatewayClient, guildId);
            await SyncAudienceAsync(guildId, GuildAudience.Guild, null, members, roster);
        }
    }

    private async Task SyncAudienceAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, IReadOnlyList<MemberOpsLevel> members, IReadOnlyDictionary<ulong, GuildUser> roster)
    {
        var roleIdsByGroup = new Dictionary<StfcOpsGroup, ulong>();
        foreach (var group in Enum.GetValues<StfcOpsGroup>())
        {
            var roleId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.OpsLevelRoles, audience, guildAllianceId, OpsLevelRolesSettingKeys.RoleForGroup(group));
            if (roleId is { } id)
                roleIdsByGroup[group] = id;
        }

        if (roleIdsByGroup.Count == 0)
            return;

        foreach (var member in members)
        {
            if (!roster.TryGetValue(member.DiscordUserId, out var guildUser))
                continue;
            var group = StfcOpsGroupExtensions.FromLevel(member.OpsLevel);
            var targetRoleId = group is { } g ? roleIdsByGroup.GetValueOrDefault(g) : (ulong?)null;
            await SyncMemberAsync(guildId, guildUser, roleIdsByGroup.Values, targetRoleId);
        }
    }

    private async Task SyncMemberAsync(ulong guildId, GuildUser guildUser, IEnumerable<ulong> allGroupRoleIds, ulong? targetRoleId)
    {
        try
        {
            foreach (var roleId in allGroupRoleIds)
            {
                var hasRole = guildUser.RoleIds.Contains(roleId);
                var shouldHaveRole = roleId == targetRoleId;

                if (shouldHaveRole && !hasRole)
                    await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, guildUser.Id, roleId);
                else if (!shouldHaveRole && hasRole)
                    await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, guildUser.Id, roleId);
            }
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "Skipped Ops level role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                guildUser.Id, guildId, ex.StatusCode);
        }
    }

    private record MemberOpsLevel(ulong DiscordUserId, int? OpsLevel);
}
