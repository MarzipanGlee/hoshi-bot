using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps each guild's configured "notification role" (NotificationRoleKind.General) in sync
// for members of any of the guild's linked alliances (GuildAlliance): removed while
// they're on an absence that suppresses notifications (starting within 15 min or already
// ongoing), added back otherwise.
public class NotificationRoleSyncJob(HoshiBotDbContext db, GatewayClient gatewayClient, ILogger<NotificationRoleSyncJob> logger) : IJob
{
    private static readonly TimeSpan LookAhead = TimeSpan.FromMinutes(15);

    public async Task Execute(IJobExecutionContext context)
    {
        var notificationRoles = await db.NotificationRoles
            .Where(r => r.Kind == NotificationRoleKind.General)
            .ToListAsync();

        foreach (var notificationRole in notificationRoles)
        {
            await SyncGuildAsync(notificationRole);
        }
    }

    private async Task SyncGuildAsync(NotificationRole notificationRole)
    {
        var ourAllianceIds = await db.GuildAlliances
            .Where(ga => ga.GuildId == notificationRole.GuildId)
            .Select(ga => ga.StfcAllianceId)
            .ToListAsync();
        if (ourAllianceIds.Count == 0)
            return;

        var memberIds = await db.GuildMembers
            .Where(gm => gm.GuildId == notificationRole.GuildId)
            .Where(gm => gm.User.PlayerLinks.Any(up => up.IsMain && up.StfcPlayer.AllianceId != null && ourAllianceIds.Contains(up.StfcPlayer.AllianceId.Value)))
            .Select(gm => gm.DiscordUserId)
            .ToListAsync();

        if (memberIds.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Add(LookAhead);

        var suppressedMemberIds = (await db.Absences
            .Where(a => a.GuildId == notificationRole.GuildId && a.SuppressNotifications && a.Status == AbsenceStatus.Confirmed
                && a.StartsAt <= cutoff && a.EndsAt > now)
            .Select(a => a.DiscordUserId)
            .ToListAsync())
            .ToHashSet();

        foreach (var userId in memberIds)
        {
            await SyncRoleAsync(notificationRole.GuildId, userId, notificationRole.DiscordRoleId,
                shouldHaveRole: !suppressedMemberIds.Contains(userId));
        }
    }

    private async Task SyncRoleAsync(ulong guildId, ulong userId, ulong roleId, bool shouldHaveRole)
    {
        try
        {
            var guildUser = await gatewayClient.Rest.GetGuildUserAsync(guildId, userId);
            var hasRole = guildUser.RoleIds.Contains(roleId);

            if (shouldHaveRole && !hasRole)
                await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, userId, roleId);
            else if (!shouldHaveRole && hasRole)
                await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, userId, roleId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "Skipped notification role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                userId, guildId, ex.StatusCode);
        }
    }
}
