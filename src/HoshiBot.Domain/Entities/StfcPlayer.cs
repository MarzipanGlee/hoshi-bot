namespace HoshiBot.Domain.Entities;

public class StfcPlayer
{
    public int Id { get; set; }

    // The external stats site's stable player ID — lets a future re-sync match/update
    // the same player instead of creating duplicates, since in-game names change over
    // time (see NameHistory). Not a Discord/game-native ID, just this one source's.
    public long ExternalId { get; set; }

    public required string Name { get; set; }

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public int? AllianceId { get; set; }

    public StfcAlliance? Alliance { get; set; }

    public ICollection<UserPlayer> UserLinks { get; set; } = [];

    public ICollection<StfcPlayerNameHistory> NameHistory { get; set; } = [];
}
