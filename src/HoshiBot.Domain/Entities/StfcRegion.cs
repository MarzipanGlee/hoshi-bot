namespace HoshiBot.Domain.Entities;

public class StfcRegion
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public ICollection<StfcVeilGroup> VeilGroups { get; set; } = [];

    public ICollection<StfcServer> Servers { get; set; } = [];
}
