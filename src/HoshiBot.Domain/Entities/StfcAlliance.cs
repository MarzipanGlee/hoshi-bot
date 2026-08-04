namespace HoshiBot.Domain.Entities;

// Alliances are server-specific in STFC and shared/global across every guild that
// tracks them — no GuildId/IsOwnAlliance/role-ID fields here, those are guild-specific
// concerns living on GuildAlliance instead (a guild can link any number of alliances).
// No DiplomacyStatus either — that's an in-game fact between two alliances, see
// StfcAllianceDiplomacy.
public class StfcAlliance
{
    public int Id { get; set; }

    // The external stats site's stable alliance ID — lets a future re-sync match/update
    // the same alliance instead of creating duplicates, since Tag/Name can both
    // legitimately change (see NameHistory). Not a Discord/game-native ID, just this one
    // source's. Long for the same reason as StfcPlayer.ExternalId: values exceed int.MaxValue.
    public long ExternalId { get; set; }

    public required string Tag { get; set; }

    public required string Name { get; set; }

    // Searchable forms of Tag and Name — see StfcPlayer.NameKey. Null, never "", when nothing in
    // the source string is typeable.
    public string? NameKey { get; set; }

    public string? TagKey { get; set; }

    // The alliance's in-game emblem, as a 0-based index into the bundled emblem image set
    // (HoshiBot.Web/wwwroot/images/emblems/emblem_{Emblem:D3}.png, currently 0–26). Null = unknown
    // (the ~10k seeded alliances have no emblem yet). Expected to line up 1:1 with stfc.pro's
    // `emblem` integer, but that mapping is unconfirmed and stfc.pro's /api/ is not permitted
    // for automated polling — so today this is set visually via the alliance admin UI (the
    // admin picks the image they see), which sidesteps the external-mapping question entirely.
    public int? Emblem { get; set; }

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public ICollection<StfcPlayer> Players { get; set; } = [];

    public ICollection<StfcTerritoryOwnership> TerritoryOwnerships { get; set; } = [];

    public ICollection<StfcAllianceDiscordInvite> DiscordInvites { get; set; } = [];

    public ICollection<StfcAllianceNameHistory> NameHistory { get; set; } = [];
}
