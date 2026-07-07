namespace HoshiBot.Domain.Entities;

public class StfcServer
{
    // The server's real Scopely numeric identifier (e.g. "US 8", "EU 164") — globally
    // unique across every server regardless of region. Assigned explicitly (never
    // auto-generated), not an arbitrary EF sequence.
    public int Id { get; set; }

    public required string Name { get; set; }

    public int RegionId { get; set; }

    public StfcRegion Region { get; set; } = null!;

    // Nullable: newly-launched servers can exist before players are able to fly to a veil
    // group area, so a server isn't always assigned to one yet.
    public int? VeilGroupId { get; set; }

    public StfcVeilGroup? VeilGroup { get; set; }

    public ICollection<StfcAlliance> Alliances { get; set; } = [];

    public ICollection<StfcPlayer> Players { get; set; } = [];

    public ICollection<StfcServerDiscordInvite> DiscordInvites { get; set; } = [];

    // "{Region}{Id} {Name}" e.g. "EU164 Mindmeld" — the display convention used
    // everywhere a server shows up in a list or dropdown (Region must be loaded/Included).
    public string DisplayName => $"{Region?.Name}{Id} {Name}";
}
