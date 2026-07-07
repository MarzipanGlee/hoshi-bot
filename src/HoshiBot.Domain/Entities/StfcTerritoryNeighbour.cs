namespace HoshiBot.Domain.Entities;

// Directed zone-adjacency edge, seeded exactly as listed per zone in the source data
// (not auto-mirrored). Used to show a zone's neighbours' current owners in the
// Territory Capture digest.
public class StfcTerritoryNeighbour
{
    public int Id { get; set; }

    public int TerritoryId { get; set; }

    public StfcTerritory Territory { get; set; } = null!;

    public int NeighbourTerritoryId { get; set; }

    public StfcTerritory NeighbourTerritory { get; set; } = null!;
}
