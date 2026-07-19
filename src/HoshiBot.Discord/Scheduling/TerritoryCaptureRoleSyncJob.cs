using System.Net;
using HoshiBot.Data;
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
    GuildFeatureSettingsService settingsService,
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

            // Fetch the roster once (bulk) instead of a GetGuildUserAsync per member.
            var roster = await GuildRoster.FetchAsync(gatewayClient, guildId);

            // Materialize this guild's absences once, then check overlap in-memory across the
            // alliance × slot × member loop below instead of querying the DB per member.
            var absences = await db.Absences.Where(a => a.GuildId == guildId).ToListAsync();

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
