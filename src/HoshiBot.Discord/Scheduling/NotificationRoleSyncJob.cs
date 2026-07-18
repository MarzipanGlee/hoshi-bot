using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps each linked alliance's configured "notification role" (the per-alliance Absences
// NotificationRole setting) in sync for that alliance's members: removed while they're on an
// absence that suppresses notifications (starting within 15 min or already ongoing), added
// back otherwise. The role is owned by the Absences feature but pinged by the Territory
// Capture weekly digest and Announcements — see AbsencesSettingKeys.NotificationRole.
public class NotificationRoleSyncJob(HoshiBotDbContext db, GatewayClient gatewayClient, ILogger<NotificationRoleSyncJob> logger) : IJob
{
    private static readonly TimeSpan LookAhead = TimeSpan.FromMinutes(15);

    public async Task Execute(IJobExecutionContext context)
    {
        var settings = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.Feature == GuildFeature.Absences
                && s.Audience == GuildAudience.Alliance
                && s.Key == AbsencesSettingKeys.NotificationRole
                && s.GuildAllianceId != null)
            .Select(s => new { s.GuildId, GuildAllianceId = s.GuildAllianceId!.Value, RoleId = s.Value })
            .ToListAsync();

        foreach (var setting in settings)
        {
            await SyncAllianceAsync(setting.GuildId, setting.GuildAllianceId, setting.RoleId);
        }
    }

    private async Task SyncAllianceAsync(ulong guildId, int guildAllianceId, ulong roleId)
    {
        var stfcAllianceId = await db.GuildAlliances
            .Where(ga => ga.Id == guildAllianceId)
            .Select(ga => (int?)ga.StfcAllianceId)
            .FirstOrDefaultAsync();
        if (stfcAllianceId is not { } allianceId)
            return;

        var memberIds = await db.GuildMembers
            .Where(gm => gm.GuildId == guildId)
            .Where(gm => gm.User.PlayerLinks.Any(up => up.IsMain && up.StfcPlayer.AllianceId == allianceId))
            .Select(gm => gm.DiscordUserId)
            .ToListAsync();

        if (memberIds.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Add(LookAhead);

        var suppressedMemberIds = (await db.Absences
            .Where(a => a.GuildId == guildId && a.SuppressNotifications && a.Status == AbsenceStatus.Confirmed
                && a.StartsAt <= cutoff && a.EndsAt > now)
            .Select(a => a.DiscordUserId)
            .ToListAsync())
            .ToHashSet();

        foreach (var userId in memberIds)
        {
            await SyncRoleAsync(guildId, userId, roleId,
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
