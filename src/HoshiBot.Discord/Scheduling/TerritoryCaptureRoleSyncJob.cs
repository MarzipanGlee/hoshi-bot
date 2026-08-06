using System.Net;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.Permissions;
using HoshiBot.Discord.TerritoryCapture;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Keeps each guild's configured zone-slot roles (TerritoryCaptureSettingKeys.ZoneSlotRole,
// slots 1-5 — the alliance's "Zone Slots: N/5" cap, not a day of the week) in sync:
// slot N's role goes to every guild member who has no Absence overlapping that slot's zone
// window this week (mirrors legacy's update-tc-roles.yag "take role, TC does not exist" /
// absence-overlap branches). Reuses TerritoryCaptureDigestService's slot computation so
// role assignment and the digest always agree on ordering.
public class TerritoryCaptureRoleSyncJob(
    HoshiBotDbContext db,
    TerritoryCaptureDigestService digestService,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PermissionGuard permissionGuard,
    NotificationDispatcher dispatcher,
    ILogger<TerritoryCaptureRoleSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var weekStart = TerritoryCaptureScheduler.GetWeekStart(DateTimeOffset.UtcNow);

        // Same guild set TerritoryCaptureDigestService.GetEligibleGuildIdsAsync uses — TC
        // only matters for guilds managing an alliance/territory ownership.
        var guildIds = await db.GuildAlliances.Select(ga => ga.GuildId).Distinct().ToListAsync();

        foreach (var guildId in guildIds)
        {
            // Each TC-enabled alliance has its own 5 zone-slot roles over its own owned zones.
            var links = await digestService.GetTcEnabledLinksAsync(guildId);
            if (links.Count == 0)
                continue;

            // Checked before the roster fetch: five zone-slot roles per alliance across the whole
            // roster is a lot of identical 403s if Manage Roles is missing.
            if (permissionGuard.For(guildId) is { CanManageRoles: false })
            {
                permissionGuard.LogSkip(guildId, "Territory Capture zone-slot roles need Manage Roles, which the bot's role doesn't have");
                await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.SyncRoles, null, BotPermission.ManageRoles);
                continue;
            }

            NotificationDispatcher.ClearPermissionIssue(guildId, BotAction.SyncRoles, null);

            // Fetch the roster once (bulk) instead of a GetGuildUserAsync per member.
            var roster = await GuildRoster.FetchAsync(gatewayClient, guildId);

            // Materialize this guild's absences once, then check overlap in-memory across the
            // alliance × slot × member loop below instead of querying the DB per member.
            var absences = await db.Absences.Where(a => a.GuildId == guildId).ToListAsync();

            // The guild-wide rank roles that grant the in-game "Activate Services" permission
            // (Admiral + Commodore) — a member with EITHER qualifies for the Services role sync
            // below. Read once per guild; skip whichever isn't configured.
            var officerRankRoleIds = new List<ulong>();
            foreach (var rankKey in new[] { RankRolesSettingKeys.AdmiralRole, RankRolesSettingKeys.CommodoreRole })
            {
                if (await settingsService.GetSnowflakeAsync(guildId, GuildFeature.RankRoles, GuildAudience.Guild, null, rankKey) is { } rankRoleId)
                    officerRankRoleIds.Add(rankRoleId);
            }

            foreach (var link in links)
            {
                var slots = await digestService.GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart);
                var slotsByIndex = slots.ToDictionary(s => s.SlotIndex);

                for (var slotIndex = 1; slotIndex <= 5; slotIndex++)
                {
                    var slotRoleId = await settingsService.GetSnowflakeAsync(
                        guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, link.Id, TerritoryCaptureSettingKeys.ZoneSlotRole(slotIndex));
                    if (slotRoleId is not { } roleId)
                        continue;

                    var hasSlot = slotsByIndex.TryGetValue(slotIndex, out var slot);

                    // Iterate EVERY member (not just this alliance's) so the slot role is removed from
                    // anyone who shouldn't have it — including non-members that a previous run wrongly
                    // gave it to. Only a holder of this alliance's member role, covering the slot, and
                    // not absent over its window, should keep it.
                    foreach (var guildUser in roster.Values)
                    {
                        var isAllianceMember = link.MemberRoleId is { } memberRoleId && guildUser.RoleIds.Contains(memberRoleId);
                        var shouldHaveRole = isAllianceMember && hasSlot &&
                            !absences.Any(a => a.DiscordUserId == guildUser.Id && a.StartsAt < slot.End && a.EndsAt > slot.Start);

                        await SyncRoleAsync(guildId, guildUser, roleId, shouldHaveRole);
                    }
                }

                // Services Role Sync (separate opt-in feature): when enabled for this alliance, mirror
                // the Admiral/Commodore rank roles onto its TC Services role — alliance members only.
                // Full sync: add to officers, remove from anyone holding it who is no longer an officer
                // / no longer an alliance member. Both roles are the same values shown on the Territory
                // Capture / Alliance Settings pages (reads a fresh roster, so it reflects the latest
                // rank-role sync).
                if (officerRankRoleIds.Count > 0 &&
                    link.MemberRoleId is { } servicesMemberRoleId &&
                    await featureService.IsEnabledAsync(guildId, GuildFeature.ServicesRoleSync, GuildAudience.Alliance, link.Id))
                {
                    var servicesRoleId = await settingsService.GetSnowflakeAsync(
                        guildId, GuildFeature.TerritoryCaptureServiceReminders, GuildAudience.Alliance, link.Id,
                        TerritoryCaptureServiceRemindersSettingKeys.ServicesRole);
                    if (servicesRoleId is { } svcRole)
                    {
                        foreach (var guildUser in roster.Values)
                        {
                            var isOfficer = officerRankRoleIds.Any(r => guildUser.RoleIds.Contains(r));
                            var shouldHaveRole = guildUser.RoleIds.Contains(servicesMemberRoleId) && isOfficer;
                            await SyncRoleAsync(guildId, guildUser, svcRole, shouldHaveRole);
                        }
                    }
                }
            }
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
                "Skipped TC role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                guildUser.Id, guildId, ex.StatusCode);
        }
    }
}
