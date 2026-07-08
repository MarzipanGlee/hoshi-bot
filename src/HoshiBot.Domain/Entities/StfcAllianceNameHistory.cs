namespace HoshiBot.Domain.Entities;

// One row per (Tag, Name) pair an alliance has been observed under, timestamped when
// first seen — mirrors StfcPlayerNameHistory, but for an alliance's two identity fields
// (rename and re-tag are both rare but real). A resync should only add a row here when
// the observed Tag or Name actually differs from the alliance's current values, not on
// every sync regardless.
public class StfcAllianceNameHistory
{
    public int Id { get; set; }

    public int StfcAllianceId { get; set; }

    public StfcAlliance StfcAlliance { get; set; } = null!;

    public required string Tag { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
