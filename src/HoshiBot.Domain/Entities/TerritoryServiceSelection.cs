namespace HoshiBot.Domain.Entities;

// One alliance's curation of a zone's services into a priority bucket — which of the services a
// zone offers (StfcTerritoryServiceSlot, synced from territory.lol) that alliance wants activated,
// and whether it's must-have or nice-to-have. Drives the two grouped lists in the "Dienste
// aktivieren" reminder. Scoped per GuildAlliance link (like every other per-alliance TC setting).
// No ordering column — the reminder orders each group by the canonical slot Position.
public class TerritoryServiceSelection
{
    public int Id { get; set; }

    public int GuildAllianceId { get; set; }

    public GuildAlliance GuildAlliance { get; set; } = null!;

    public int TerritoryId { get; set; }

    public StfcTerritory Territory { get; set; } = null!;

    public long ServiceId { get; set; }

    public StfcTerritoryService Service { get; set; } = null!;

    public TerritoryServicePriority Priority { get; set; }
}
