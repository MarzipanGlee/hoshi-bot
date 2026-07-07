namespace HoshiBot.Domain.Entities;

// One of the small, fixed set of contestable territory-capture zones. Not a single
// StfcSystem — a zone groups 2-4 real systems named "{ZoneName} {Suffix}" (e.g.
// "Hoobishan" groups "Hoobishan Alpha"/"Hoobishan Beta"), linked via StfcSystem.TerritoryId.
// Weekday/CaptureTimeUtc are nullable — some zones' current schedule isn't known yet
// (not recently captured); Tier is a per-zone property (not derived from Weekday — the
// legacy per-weekday tier formula broke once a Tier-4 zone was introduced).
public class StfcTerritory
{
    // The real Scopely/stfc.pro territory ID (see https://api.stfc.pro/stfc_territories) —
    // assigned explicitly by the seeder, never auto-generated.
    public int Id { get; set; }

    public required string Name { get; set; }

    public int Tier { get; set; }

    public DayOfWeek? Weekday { get; set; }

    public TimeOnly? CaptureTimeUtc { get; set; }

    public ICollection<StfcSystem> Systems { get; set; } = [];

    public ICollection<StfcTerritoryOwnership> Ownerships { get; set; } = [];
}
