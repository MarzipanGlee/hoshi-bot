using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps each guild's configured rank roles (RankRolesSettingKeys.{Admiral,Commodore,Premier,
// Operative,Agent}Role) in sync with each member's current STFC rank (StfcPlayer.Rank, set by
// a player-data import — see StfcPlayerImportService): the one role matching their rank is
// added, the other 4 are removed if present, so a promotion/demotion swaps the role instead
// of accumulating old ones. Runs per enabled audience independently, since RankRoles (unlike
// TerritoryCapture/NotificationRole) is available to all 4 audiences and each can have its
// own set of 5 roles configured.
public class RankRoleSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    ILogger<RankRoleSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var guildIds = await db.GuildEnabledFeatures
            .Where(f => f.Feature == GuildFeature.RankRoles)
            .Select(f => f.GuildId)
            .Distinct()
            .ToListAsync();

        foreach (var guildId in guildIds)
        {
            var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.RankRoles);
            if (enabledAudiences.Count == 0)
                continue;

            var members = await db.GuildMembers
                .Where(gm => gm.GuildId == guildId)
                .Select(gm => new MemberRank(
                    gm.DiscordUserId,
                    gm.User.PlayerLinks.Where(up => up.IsMain).Select(up => (StfcPlayerRank?)up.StfcPlayer.Rank).FirstOrDefault()))
                .ToListAsync();

            foreach (var audience in enabledAudiences)
                await SyncAudienceAsync(guildId, audience, members);
        }
    }

    private async Task SyncAudienceAsync(ulong guildId, GuildAudience audience, IReadOnlyList<MemberRank> members)
    {
        var roleIdsByRank = new Dictionary<StfcPlayerRank, ulong>();
        foreach (var rank in Enum.GetValues<StfcPlayerRank>())
        {
            var roleId = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.RankRoles, audience, RankRolesSettingKeys.RoleForRank(rank));
            if (roleId is { } id)
                roleIdsByRank[rank] = id;
        }

        if (roleIdsByRank.Count == 0)
            return;

        foreach (var member in members)
        {
            var targetRoleId = member.Rank is { } rank ? roleIdsByRank.GetValueOrDefault(rank) : (ulong?)null;
            await SyncMemberAsync(guildId, member.DiscordUserId, roleIdsByRank.Values, targetRoleId);
        }
    }

    private async Task SyncMemberAsync(ulong guildId, ulong userId, IEnumerable<ulong> allRankRoleIds, ulong? targetRoleId)
    {
        try
        {
            var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
            foreach (var roleId in allRankRoleIds)
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
                "Skipped rank role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                userId, guildId, ex.StatusCode);
        }
    }

    private record MemberRank(ulong DiscordUserId, StfcPlayerRank? Rank);
}
