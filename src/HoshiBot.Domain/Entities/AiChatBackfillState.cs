namespace HoshiBot.Domain.Entities;

// Per-channel progress cursor for the AI-chat knowledge index's progressive full-history backfill.
// The backfill job pages a bounded chunk further back in time each run (see
// AiChatIndexService.BackfillGuildAsync); this row records, per resolved channel/thread, whether
// its entire history has been reached so completed channels stop being paged. The backward paging
// anchor itself is derived from the data (MIN(MessageId) already indexed for the channel), so only
// the done-flag needs persisting here.
public class AiChatBackfillState
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // The concrete channel or thread id being backfilled.
    public ulong ChannelId { get; set; }

    // True once backward paging has reached the channel's oldest message.
    public bool HistoryComplete { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
