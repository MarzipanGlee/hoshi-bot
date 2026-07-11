using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps each guild's configured Ops Level roles (OpsLevelRolesSettingKeys.{G1..G7}Role) in
// sync with each member's current STFC Ops Level (StfcPlayer.OpsLevel, set by a player-data
// import — see StfcPlayerImportService), bucketed into a G1-G7 tier via
// StfcOpsGroupExtensions.FromLevel: the one role matching their tier is added, the other 6
// are removed if present, so leveling up/down swaps the role instead of accumulating old
// ones. Runs per enabled audience independently, since OpsLevelRoles (like RankRoles) is
// available to all 4 audiences and each can have its own set of 7 roles configured.
public class OpsLevelRoleSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    ILogger<OpsLevelRoleSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var guildIds = await db.GuildEnabledFeatures
            .Where(f => f.Feature == GuildFeature.OpsLevelRoles)
            .Select(f => f.GuildId)
            .Distinct()
            .ToListAsync();

        foreach (var guildId in guildIds)
        {
            var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.OpsLevelRoles);
            if (enabledAudiences.Count == 0)
                continue;

            var members = await db.GuildMembers
                .Where(gm => gm.GuildId == guildId)
                .Select(gm => new MemberOpsLevel(
                    gm.DiscordUserId,
                    gm.User.PlayerLinks.Where(up => up.IsMain).Select(up => (int?)up.StfcPlayer.OpsLevel).FirstOrDefault()))
                .ToListAsync();

            foreach (var audience in enabledAudiences)
                await SyncAudienceAsync(guildId, audience, members);
        }
    }

    private async Task SyncAudienceAsync(ulong guildId, GuildAudience audience, IReadOnlyList<MemberOpsLevel> members)
    {
        var roleIdsByGroup = new Dictionary<StfcOpsGroup, ulong>();
        foreach (var group in Enum.GetValues<StfcOpsGroup>())
        {
            var roleId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.OpsLevelRoles, audience, OpsLevelRolesSettingKeys.RoleForGroup(group));
            if (roleId is { } id)
                roleIdsByGroup[group] = id;
        }

        if (roleIdsByGroup.Count == 0)
            return;

        foreach (var member in members)
        {
            var group = StfcOpsGroupExtensions.FromLevel(member.OpsLevel);
            var targetRoleId = group is { } g ? roleIdsByGroup.GetValueOrDefault(g) : (ulong?)null;
            await SyncMemberAsync(guildId, member.DiscordUserId, roleIdsByGroup.Values, targetRoleId);
        }
    }

    private async Task SyncMemberAsync(ulong guildId, ulong userId, IEnumerable<ulong> allGroupRoleIds, ulong? targetRoleId)
    {
        try
        {
            var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
            foreach (var roleId in allGroupRoleIds)
            {
                var hasRole = guildUser.RoleIds.Contains(roleId);
                var shouldHaveRole = roleId == targetRoleId;

                if (shouldHaveRole && !hasRole)
                    await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, userId, roleId);
                else if (!shouldHaveRole && hasRole)
                    await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, userId, roleId);
            }
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "Skipped Ops level role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                userId, guildId, ex.StatusCode);
        }
    }

    private record MemberOpsLevel(ulong DiscordUserId, int? OpsLevel);
}
