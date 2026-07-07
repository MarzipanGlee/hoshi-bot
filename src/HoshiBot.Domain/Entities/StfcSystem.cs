namespace HoshiBot.Domain.Entities;

// Global reference catalog of every named system in the game. Synced daily from
// https://data.stfc.space/translations/en/systems.json by StfcSystemSyncService (in
// HoshiBot.Web) — see there for the sync/seed logic. Most systems have no ownership
// concept at all; StfcTerritory marks the small subset that do.
public class StfcSystem
{
    public int Id { get; set; }

    // The system's numeric ID from the source data — globally unique, used to match rows
    // across daily syncs (renames get picked up, new systems get added).
    public int Number { get; set; }

    public required string Name { get; set; }

    // Whether the system supports "Station Housing" (players can park a station there) —
    // stfc.space's site shows this as a "Housing" badge, driven by the source data's
    // has_player_containers field (traced via stfc.space's own bundled JS).
    public bool HasStationHousing { get; set; }

    // Which territory-capture zone this system belongs to, if any (most systems have
    // none) — kept in sync by StfcSystemSyncService matching "{Territory.Name} {Suffix}".
    public int? TerritoryId { get; set; }

    public StfcTerritory? Territory { get; set; }
}
