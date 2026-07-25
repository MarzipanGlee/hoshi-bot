namespace HoshiBot.Domain.Entities;

// One Territory Capture "service" (Dienst) — a buff/resource an alliance can activate on a
// zone it holds during a takeover. A global catalog (identical across all servers/regions),
// synced from territory.lol's `territory_service_specs` + `translation.json`. Which services a
// given zone offers is per-server and lives in StfcTerritoryServiceSlot.
public class StfcTerritoryService
{
    // The real Scopely service spec id (the `svid` key in territory_service_specs) — assigned
    // explicitly by the sync, never auto-generated. A long, not int: some svids exceed int32.
    public long Id { get; set; }

    // The localisation id (id_refs.loca_id) the Name/Description/InfoShort resolve through.
    public int LocaId { get; set; }

    // Resolved from translation.json's services_name_{loca} (English game term).
    public required string Name { get; set; }

    // services_description_{loca} — the long bonus description (may be null for a few specs).
    public string? Description { get; set; }

    // services_info_short_{loca} — a short one-line summary.
    public string? InfoShort { get; set; }

    public int Rarity { get; set; }

    public ICollection<StfcTerritoryServiceSlot> Slots { get; set; } = [];
}
