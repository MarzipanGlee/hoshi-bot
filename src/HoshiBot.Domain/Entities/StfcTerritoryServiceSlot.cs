namespace HoshiBot.Domain.Entities;

// Which service a given zone offers on a given server, and in what order — the per-server
// territory→service mapping synced from territory.lol's service_slots_{serverId}.json
// (`{ territories: { territoryId: [svid, …] } }`). Different servers assign different services
// to the same zone, hence the ServerId dimension. Position preserves the in-game slot order.
public class StfcTerritoryServiceSlot
{
    public int Id { get; set; }

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public int TerritoryId { get; set; }

    public StfcTerritory Territory { get; set; } = null!;

    public long ServiceId { get; set; }

    public StfcTerritoryService Service { get; set; } = null!;

    // 0-based order within the zone's service list on this server (the sequence in which the
    // Services reminder lists them).
    public int Position { get; set; }
}
