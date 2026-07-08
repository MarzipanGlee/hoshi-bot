namespace HoshiBot.Domain.Entities;

// One row per name a player has been observed under, timestamped when first seen — lets
// Discord nickname sync detect a rename instead of only ever knowing the current name,
// and gives an audit trail if a player's identity needs verifying later. A resync should
// only add a row here when the observed name actually differs from the player's current
// Name, not on every sync regardless.
public class StfcPlayerNameHistory
{
    public int Id { get; set; }

    public int StfcPlayerId { get; set; }

    public StfcPlayer StfcPlayer { get; set; } = null!;

    public required string Name { get; set; }

    public DateTimeOffset ObservedAt { get; set; }
}
