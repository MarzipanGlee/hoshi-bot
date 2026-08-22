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

    // "{Region}-{Id} {Name}" e.g. "EU-164 Mindmeld" — the display convention used everywhere a
    // server shows up in a list or dropdown. Degrades to "164 Mindmeld" when Region was not
    // loaded/Included, rather than showing a leading dash.
    public string DisplayName => $"{RegionServer(Region?.Name, Id)} {Name}";

    // The region and the server number as one designation: "EU-164". Hyphenated because they are
    // two facts — "EU164" reads as a single token, and the pair shows up in nicknames, breadcrumbs
    // and dropdowns, where scanning it at a glance is the whole job.
    //
    // Here rather than inlined at each site so the three renderers cannot drift: this, the alliance
    // breadcrumb, and NicknameComposer's server tag. They already had, which is why one page still
    // said "EU164" after the others changed.
    // No region, no separator. A caller that did not Include the region used to get "-164", a bare
    // dash with nothing in front of it — visible on Import Players, whose RegionServerPicker filters
    // by region and so never loaded it. The number alone is also the right label there: the region
    // is already named in the field beside it.
    public static string RegionServer(string? regionName, int serverId) =>
        string.IsNullOrWhiteSpace(regionName) ? $"{serverId}" : $"{regionName}-{serverId}";
}
