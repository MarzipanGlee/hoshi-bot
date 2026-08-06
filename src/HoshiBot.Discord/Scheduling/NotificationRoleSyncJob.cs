using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.Permissions;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps each linked alliance's configured "notification role" (the per-alliance Absences
// NotificationRole setting) in sync for that alliance's members: removed while they're on an
// absence that suppresses notifications (starting within 15 min or already ongoing), added
// back otherwise. The role is owned by the Absences feature but pinged by the Territory
// Capture weekly digest and Announcements — see AbsencesSettingKeys.NotificationRole.
public class NotificationRoleSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    PlayerLinkService playerLinkService,
    PermissionGuard permissionGuard,
    NotificationDispatcher dispatcher,
    ILogger<NotificationRoleSyncJob> logger) : IJob
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

        // Fetch each guild's roster once (bulk) and reuse it across that guild's alliances, instead of
        // a GetGuildUserAsync per member.
        foreach (var guildGroup in settings.GroupBy(s => s.GuildId))
        {
            // Checked before the roster fetch: a guild without Manage Roles would otherwise 403 once
            // per member per alliance, every 10 minutes, forever.
            if (permissionGuard.For(guildGroup.Key) is { CanManageRoles: false })
            {
                permissionGuard.LogSkip(guildGroup.Key, "Absence notification roles need Manage Roles, which the bot's role doesn't have");
                await dispatcher.NotifyAdminOfPermissionIssueAsync(guildGroup.Key, BotAction.SyncRoles, null, BotPermission.ManageRoles);
                continue;
            }

            NotificationDispatcher.ClearPermissionIssue(guildGroup.Key, BotAction.SyncRoles, null);

            var roster = await GuildRoster.FetchAsync(gatewayClient, guildGroup.Key);
            // Resolved once per guild, not per alliance: which player represents each member here.
            var primaries = await playerLinkService.GetGuildPrimaryPlayersAsync(guildGroup.Key);
            foreach (var setting in guildGroup)
                await SyncAllianceAsync(setting.GuildId, setting.GuildAllianceId, setting.RoleId, roster, primaries);
        }
    }

    private async Task SyncAllianceAsync(ulong guildId, int guildAllianceId, ulong roleId, IReadOnlyDictionary<ulong, GuildUser> roster, IReadOnlyDictionary<ulong, GuildPrimaryPlayer> primaries)
    {
        var stfcAllianceId = await db.GuildAlliances
            .Where(ga => ga.Id == guildAllianceId)
            .Select(ga => (int?)ga.StfcAllianceId)
            .FirstOrDefaultAsync();
        if (stfcAllianceId is not { } allianceId)
            return;

        var memberIds = primaries.Values
            .Where(p => p.AllianceId == allianceId)
            .Select(p => p.DiscordUserId)
            .ToList();

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
            if (!roster.TryGetValue(userId, out var guildUser))
                continue;
            await SyncRoleAsync(guildId, guildUser, roleId,
                shouldHaveRole: !suppressedMemberIds.Contains(userId));
        }
    }

    private async Task SyncRoleAsync(ulong guildId, GuildUser guildUser, ulong roleId, bool shouldHaveRole)
    {
        try
        {
            var hasRole = guildUser.RoleIds.Contains(roleId);

            if (shouldHaveRole && !hasRole)
                await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, guildUser.Id, roleId);
            else if (!shouldHaveRole && hasRole)
                await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, guildUser.Id, roleId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "Skipped notification role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                guildUser.Id, guildId, ex.StatusCode);
        }
    }
}
