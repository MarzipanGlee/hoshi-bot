namespace HoshiBot.Domain.Entities;

// One row per StfcRegion — the preserved daily "time of day" Infinite Incursions starts in
// that region, which an admin-submitted date is combined with to produce that region's
// EventStart. Editable by global admins since these have changed before and may again
// (confirmed real values at seed time: US 15:00 UTC, EU 08:00 UTC, APAC 23:00 UTC).
public class IncursionsRegionDefault
{
    public int Id { get; set; }

    public int RegionId { get; set; }

    public StfcRegion Region { get; set; } = null!;

    public TimeOnly DefaultStartTimeUtc { get; set; }
}
