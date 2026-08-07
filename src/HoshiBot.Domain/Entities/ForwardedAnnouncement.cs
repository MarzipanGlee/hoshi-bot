namespace HoshiBot.Domain.Entities;

// Tracks one announcement the Announcement Forwarder has translated and posted, so it can (a) tell
// "already forwarded" from "missed, needs catching up" (AnnouncementForwarderCatchUpJob) and (b) find
// the destination message to edit in place when the source is edited (AiChatIndexReconcileHandler).
// One row per source message. See the forwarder's plan docs.
public class ForwardedAnnouncement
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong SourceChannelId { get; set; }

    // The Discord message id of the original (source) announcement. NOT unique on its own: since the
    // forwarder became audience-scoped, one source announcement can legitimately reach several
    // destinations — a coalition guild forwarding the same source into each alliance's own channel.
    // The natural key is (SourceMessageId, DestinationChannelId); keying on the message alone silently
    // suppressed every destination after the first.
    public ulong SourceMessageId { get; set; }

    public ulong DestinationChannelId { get; set; }

    public ulong DestinationMessageId { get; set; }

    // A stable hash of the rendered source text at last-forward time — lets the edit path tell a
    // real content change from a cosmetic MESSAGE_UPDATE (e.g. Discord's own link-embed refresh) and
    // skip a pointless re-translate.
    public string SourceContentHash { get; set; } = "";

    public DateTimeOffset ForwardedAt { get; set; }

    // Set when the source was edited and the destination message was updated in place.
    public DateTimeOffset? UpdatedAt { get; set; }
}
