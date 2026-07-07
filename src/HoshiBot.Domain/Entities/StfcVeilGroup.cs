namespace HoshiBot.Domain.Entities;

// Currently only about mapping a server to its veil group — no veil-specific
// systems/ownership modeled yet, that's deferred until actually needed.
public class StfcVeilGroup
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int RegionId { get; set; }

    public StfcRegion Region { get; set; } = null!;

    public ICollection<StfcServer> Servers { get; set; } = [];

    public ICollection<StfcVeilGroupDiscordInvite> DiscordInvites { get; set; } = [];
}
