namespace HoshiBot.Domain.Entities;

// Latest known state for a recurring STFC event category (e.g. "incursions"), keyed by
// that category name directly — a small, fixed set of known groups, not something
// needing a surrogate Id. Same Notified*/observed split and temporary-seed situation as
// StfcServerStatus (see there for why).
//
// Only "incursions" is currently acted on (see InfiniteIncursionsNotifyJob) — the other groups
// are stored for completeness but have no notify logic yet.
public class StfcEventStatus
{
    public required string EventGroup { get; set; }

    public DateTimeOffset EventStart { get; set; }

    public DateTimeOffset? EventEnd { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // The advance-warning trigger is "EventStart changed to a new future date we haven't
    // already warned about" — not a simple observed/notified equality check like
    // StfcServerStatus, since the same past EventStart shouldn't re-trigger anything.
    public DateTimeOffset? NotifiedEventStart { get; set; }
}
