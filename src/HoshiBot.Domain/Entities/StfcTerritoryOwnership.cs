namespace HoshiBot.Domain.Entities;

// Uniqueness on (TerritoryId, ServerId) is enforced in application code, not a DB
// constraint — avoids cross-provider filtered-index syntax differences between
// Postgres/SQLite for a personal-scale bot.
public class StfcTerritoryOwnership
{
    public int Id { get; set; }

    public int TerritoryId { get; set; }

    public StfcTerritory Territory { get; set; } = null!;

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public int AllianceId { get; set; }

    public StfcAlliance Alliance { get; set; } = null!;

    public DateTimeOffset? LastCapturedAt { get; set; }
}
