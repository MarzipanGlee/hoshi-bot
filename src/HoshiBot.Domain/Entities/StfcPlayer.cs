namespace HoshiBot.Domain.Entities;

public class StfcPlayer
{
    public int Id { get; set; }

    // The external stats site's stable player ID — lets a future re-sync match/update
    // the same player instead of creating duplicates, since in-game names change over
    // time (see NameHistory). Not a Discord/game-native ID, just this one source's.
    public long ExternalId { get; set; }

    public required string Name { get; set; }

    // What a Latin keyboard would type to find this player (PlayerNameKey.Compute), maintained by
    // HoshiBotDbContext.SaveChanges. Null — never "" — when the name has nothing typeable in it at
    // all: an empty key would match every substring search and would make all such players match
    // each other.
    public string? NameKey { get; set; }

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public int? AllianceId { get; set; }

    public StfcAlliance? Alliance { get; set; }

    // Null for unranked/unaffiliated players, or ones never seen by an import yet.
    public StfcPlayerRank? Rank { get; set; }

    // The raw 1-80 STFC Ops Level stat, not the derived G1-G7 group — see
    // StfcOpsGroupExtensions.FromLevel. Null for players never seen by an import yet.
    public int? OpsLevel { get; set; }

    public ICollection<UserPlayer> UserLinks { get; set; } = [];

    public ICollection<StfcPlayerNameHistory> NameHistory { get; set; } = [];
}
