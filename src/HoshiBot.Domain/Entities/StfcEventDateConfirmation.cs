namespace HoshiBot.Domain.Entities;

// One row per distinct Discord user who confirmed a StfcNewsPost's *current* SubmittedDate —
// find-or-create against the unique (StfcNewsPostId, DiscordUserId) index makes a repeat
// click a no-op, same idea as AnnouncementReadReceipt. Cleared out whenever a new date is
// submitted via Edit, since a changed date needs a fresh quorum.
public class StfcEventDateConfirmation
{
    public int Id { get; set; }

    public int StfcNewsPostId { get; set; }

    public StfcNewsPost StfcNewsPost { get; set; } = null!;

    public ulong DiscordUserId { get; set; }

    public DateTimeOffset ConfirmedAt { get; set; }
}
