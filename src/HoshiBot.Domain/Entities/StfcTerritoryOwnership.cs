namespace HoshiBot.Domain.Entities;

// A territory on a given server has exactly one owning alliance, so (TerritoryId, ServerId) is
// unique — enforced by a DB unique index (StfcTerritoryOwnershipConfiguration). App-code-only
// enforcement proved insufficient: HoshiBot.Host and HoshiBot.Web both run the ownership seeder
// on startup, and when they raced an empty table both inserted the full snapshot, silently
// doubling every row (and later breaking the ownership import, which keys by this pair).
public class StfcTerritoryOwnership
{
    public int Id { get; set; }

    public int TerritoryId { get; set; }

    public StfcTerritory Territory { get; set; } = null!;

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public int AllianceId { get; set; }

    public StfcAlliance Alliance { get; set; } = null!;
}
