using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;

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
    HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding)
    : DiffNotifyJobBase<StfcEventStatus>(db, dispatcher, embedBranding)
{
    // The string value ("incursions") is a real persisted lookup key (StfcEventStatus.EventGroup)
    // — do not change the value, only the constant's own name is cosmetic.
    private const string InfiniteIncursionsEventGroup = "incursions";

    protected override GuildAlertChannelKind ChannelKind => GuildAlertChannelKind.InfiniteIncursions;
    protected override GuildFeature Feature => GuildFeature.InfiniteIncursions;

    protected override async Task<List<StfcEventStatus>> LoadPendingRowsAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var regionRows = await Db.StfcEventStatuses
            .Include(e => e.Region)
            .Where(e => e.EventGroup == InfiniteIncursionsEventGroup)
            .ToListAsync();

        return regionRows
            .Where(row => row.RegionId is not null && row.EventStart != row.NotifiedEventStart && row.EventStart > now)
            .ToList();
    }

    protected override Task<List<ulong>> ResolveGuildIdsAsync(StfcEventStatus row)
    {
        var regionId = row.RegionId!.Value; // LoadPendingRowsAsync filtered out region-less rows

        return Db.GuildServers
            .Where(g => g.StfcServer.RegionId == regionId)
            .Select(g => g.GuildId)
            .Distinct()
            .ToListAsync();
    }

    protected override (string Content, NetCord.Color Color) BuildAnnouncement(StfcEventStatus row, Language lang)
    {
        var regionName = row.Region?.Name ?? "?";
        return (Msg.Event.IncursionsScheduled(lang, regionName, row.EventStart.ToUnixTimeSeconds()),
            EmbedBranding.WarningColor);
    }

    protected override void MarkNotified(StfcEventStatus row) => row.NotifiedEventStart = row.EventStart;
}
