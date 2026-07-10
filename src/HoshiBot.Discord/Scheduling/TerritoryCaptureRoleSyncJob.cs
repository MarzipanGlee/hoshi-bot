using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            if (!await featureService.IsEnabledAsync(guildId, GuildFeature.TerritoryCapture))
                continue;

            var slots = await digestService.GetWeeklySlotAssignmentsAsync(guildId, weekStart);
            var slotsByIndex = slots.ToDictionary(s => s.SlotIndex);

            var memberIds = await db.GuildMembers.Where(m => m.GuildId == guildId).Select(m => m.DiscordUserId).ToListAsync();

            // Filtered client-side: SQLite's EF Core provider can't translate DateTimeOffset
            // range comparisons here, and per-guild absence counts are always small.
            var absences = await db.Absences.Where(a => a.GuildId == guildId).ToListAsync();

            for (var slotIndex = 1; slotIndex <= 5; slotIndex++)
            {
                var slotRoleId = await settingsService.GetSnowflakeAsync(
                    guildId, GuildFeature.TerritoryCapture, GuildAudience.Alliance, TerritoryCaptureSettingKeys.ZoneSlotRole(slotIndex));
                if (slotRoleId is not { } roleId)
                    continue;

                var hasSlot = slotsByIndex.TryGetValue(slotIndex, out var slot);

                foreach (var userId in memberIds)
                {
                    var shouldHaveRole = hasSlot &&
                        !absences.Any(a => a.DiscordUserId == userId && a.StartsAt < slot.End && a.EndsAt > slot.Start);

                    await SyncRoleAsync(guildId, userId, roleId, shouldHaveRole);
                }
            }
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
                "Skipped TC role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                userId, guildId, ex.StatusCode);
        }
    }
}
