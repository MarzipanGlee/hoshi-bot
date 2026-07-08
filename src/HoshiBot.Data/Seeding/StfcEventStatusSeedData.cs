namespace HoshiBot.Data.Seeding;

// A one-time snapshot of each recurring event category's most recent occurrence,
// captured 2026-07-08 from an external STFC stats site's API. Seeded once into an
// empty table as a baseline for StfcEventStatus (see there for why this is temporary
// rather than an ongoing sync).
public static class StfcEventStatusSeedData
{
    public static readonly (string EventGroup, DateTimeOffset EventStart, DateTimeOffset? EventEnd, bool Active)[] Entries =
    [
        ("incursions", DateTimeOffset.Parse("2026-06-20T23:00:00Z"), DateTimeOffset.Parse("2026-06-21T11:00:00Z"), false),
        ("alliance_tournaments", DateTimeOffset.Parse("2026-05-05T17:00:00Z"), DateTimeOffset.Parse("2026-05-10T17:00:00Z"), false),
        ("sarris_invasions", DateTimeOffset.Parse("2025-09-29T18:00:00Z"), DateTimeOffset.Parse("2025-10-02T18:00:00Z"), false),
        ("flashpoint", DateTimeOffset.Parse("2025-08-28T18:00:00Z"), DateTimeOffset.Parse("2025-09-01T18:00:00Z"), false),
    ];
}
