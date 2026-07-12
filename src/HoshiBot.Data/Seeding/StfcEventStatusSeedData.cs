namespace HoshiBot.Data.Seeding;

// A one-time snapshot of each recurring event category's most recent occurrence, captured
// 2026-07-08 from an external STFC stats site's API, later corrected 2026-07-12 for Infinite
// Incursions. Seeded once into an empty table as a baseline for StfcEventStatus (see there for
// why this is temporary rather than an ongoing sync).
//
// Infinite Incursions is region-split — 3 rows, one per StfcRegion (RegionId: 1 = US, 2 = EU,
// 3 = APAC, per ServiceCollectionExtensions.ScopelyRegionIds) — confirmed from a real pairings
// announcement post: US 15:00 UTC, EU 08:00 UTC, APAC 23:00 UTC, each a 12-hour event. The
// original seed only carried the APAC time (23:00 UTC) as a single global value, which was
// silently wrong for US/EU guilds. Every other event group is not known to be region-split, so
// keeps a single row with RegionId = null.
public static class StfcEventStatusSeedData
{
    public static readonly (string EventGroup, int? RegionId, DateTimeOffset EventStart, DateTimeOffset? EventEnd, bool Active)[] Entries =
    [
        ("incursions", 1, DateTimeOffset.Parse("2026-06-20T15:00:00Z"), DateTimeOffset.Parse("2026-06-21T03:00:00Z"), false), // US
        ("incursions", 2, DateTimeOffset.Parse("2026-06-20T08:00:00Z"), DateTimeOffset.Parse("2026-06-20T20:00:00Z"), false), // EU
        ("incursions", 3, DateTimeOffset.Parse("2026-06-20T23:00:00Z"), DateTimeOffset.Parse("2026-06-21T11:00:00Z"), false), // APAC
        ("alliance_tournaments", null, DateTimeOffset.Parse("2026-05-05T17:00:00Z"), DateTimeOffset.Parse("2026-05-10T17:00:00Z"), false),
        ("sarris_invasions", null, DateTimeOffset.Parse("2025-09-29T18:00:00Z"), DateTimeOffset.Parse("2025-10-02T18:00:00Z"), false),
        ("flashpoint", null, DateTimeOffset.Parse("2025-08-28T18:00:00Z"), DateTimeOffset.Parse("2025-09-01T18:00:00Z"), false),
    ];
}
