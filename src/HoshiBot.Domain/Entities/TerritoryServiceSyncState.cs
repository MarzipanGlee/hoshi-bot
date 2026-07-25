namespace HoshiBot.Domain.Entities;

// Single-row change-detection state for the territory.lol service sync. meta.json exposes
// (tcSeason, generatedAt); a sync compares them against the last run and skips re-ingesting
// when unchanged (unless forced).
public class TerritoryServiceSyncState
{
    public int Id { get; set; }

    public string? TcSeason { get; set; }

    public long GeneratedAt { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}
