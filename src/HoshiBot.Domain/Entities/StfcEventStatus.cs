namespace HoshiBot.Domain.Entities;

// Latest known state for a recurring STFC event category (e.g. "incursions"). EventGroup is
// no longer the primary key — "incursions" needs 3 rows (one per StfcRegion), since Infinite
// Incursions has 3 distinct regional start times (confirmed from a real pairings post: US
// 15:00 UTC, EU 08:00 UTC, APAC 23:00 UTC), not one global time as originally assumed. Every
// other event group keeps a single row with RegionId = null. Same Notified*/observed split
// and temporary-seed situation as StfcServerStatus (see there for why).
//
// Only "incursions" is currently acted on (see InfiniteIncursionsNotifyJob) — the other groups
// are stored for completeness but have no notify logic yet.
public class StfcEventStatus
{
    public int Id { get; set; }

    public required string EventGroup { get; set; }

    // null = not region-split (every event group except "incursions" today).
    public int? RegionId { get; set; }

    public StfcRegion? Region { get; set; }

    public DateTimeOffset EventStart { get; set; }

    public DateTimeOffset? EventEnd { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // The advance-warning trigger is "EventStart changed to a new future date we haven't
    // already warned about" — not a simple observed/notified equality check like
    // StfcServerStatus, since the same past EventStart shouldn't re-trigger anything.
    public DateTimeOffset? NotifiedEventStart { get; set; }
}
