namespace HoshiBot.Domain.Entities;

// Which ReadablePostKinds actually have something that produces them. The other three are declared
// so the editor can show the intended shape, but nothing posts them yet — and an admin switching one
// on would be waiting for a post that cannot arrive, which is exactly how seven alliance channel
// pickers ended up labelled "not implemented yet" after the fact.
//
// Move a kind here in the same commit that gives it a producer.
public static class ReadablePostKinds
{
    public static readonly IReadOnlyList<ReadablePostKind> Implemented =
        [ReadablePostKind.Announcement, ReadablePostKind.ForwardedAnnouncement];

    public static bool IsImplemented(ReadablePostKind kind) => Implemented.Contains(kind);

    // Whether the button shows how many people have confirmed. An announcement's does — it is how
    // staff see uptake. A welcome message's does not: its caption is the admin's own wording, and a
    // number climbing on it would be noise on a post every new member sees exactly once.
    //
    // Also saves work: AnnouncementCounterRefreshJob edits a post only to move that number, so a
    // kind without one needs no edit at all when the count changes.
    public static bool ShowsReadCount(ReadablePostKind kind) => kind != ReadablePostKind.WelcomeMessage;
}
