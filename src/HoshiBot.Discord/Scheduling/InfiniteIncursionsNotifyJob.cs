using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Announces a newly-scheduled Infinite Incursions event as advance warning. Region-aware:
// Infinite Incursions has 3 distinct regional start times (confirmed from a real pairings
// post — US/EU/APAC), so StfcEventStatus now carries 3 rows for EventGroup == "incursions"
// (one per StfcRegion), each progressing independently. A guild is notified with a given
// region's time only if it tracks a server in that region (via GuildServer -> StfcServer ->
// Region) — a guild tracking servers in multiple regions is notified once per matching
// region; a guild with no resolvable region at all is skipped entirely (a deliberate
// behavior change from the previous region-blind "notify every GuildServer guild
// regardless"). Same one-time-seed situation as ServerStatusNotifyJob (see there for why).
public class InfiniteIncursionsNotifyJob(
    HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding) : IJob
{
    // The string value ("incursions") is a real persisted lookup key (StfcEventStatus.EventGroup)
    // — do not change the value, only the constant's own name is cosmetic.
    private const string InfiniteIncursionsEventGroup = "incursions";

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTimeOffset.UtcNow;

        var regionRows = await db.StfcEventStatuses
            .Include(e => e.Region)
            .Where(e => e.EventGroup == InfiniteIncursionsEventGroup)
            .ToListAsync();

        foreach (var row in regionRows)
        {
            if (row.RegionId is not { } regionId || row.EventStart == row.NotifiedEventStart || row.EventStart <= now)
                continue;

            var guildIds = await db.GuildServers
                .Where(g => g.StfcServer.RegionId == regionId)
                .Select(g => g.GuildId)
                .Distinct()
                .ToListAsync();

            var regionName = row.Region?.Name ?? "?";
            var content = $"⚔️ [{regionName}] A new Infinite Incursions event is scheduled to start <t:{row.EventStart.ToUnixTimeSeconds()}:R>!";

            foreach (var guildId in guildIds)
            {
                var embed = await embedBranding.BuildBrandedAsync(guildId, content, EmbedBranding.WarningColor);
                await dispatcher.SendPublicToEnabledAudiencesAsync(
                    guildId, GuildAlertChannelKind.InfiniteIncursions, GuildFeature.InfiniteIncursions, content, embed: embed);
            }

            row.NotifiedEventStart = row.EventStart;
        }

        await db.SaveChangesAsync();
    }
}
