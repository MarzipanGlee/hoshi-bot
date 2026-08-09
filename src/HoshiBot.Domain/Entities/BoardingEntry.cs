namespace HoshiBot.Domain.Entities;

// One row per member the bot has boarded, and the reason the whole feature cannot loop.
//
// It is tempting to derive "should be boarded" from state — no member role, no read receipt — but
// every such rule re-boards the people it should leave alone. A member who confirms and later leaves
// the alliance loses the member role by another job's hand, and a receipt-driven rule would then
// re-board them, re-DM them, and do it again next week. This row is the one-shot marker: a member is
// boarded once per guild, and the row's existence is what stops it happening twice.
//
// Status carries the per-member failure so one bad case advances instead of retrying forever — the
// shape CONTRIBUTING asks for, where a single misconfiguration must not turn into thousands of
// invalid requests.
public class BoardingEntry
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong DiscordUserId { get; set; }

    // The standing message this member was pointed at. Carries the scope too (audience + alliance),
    // so this row does not repeat it.
    public int ReadablePostId { get; set; }

    public ReadablePost ReadablePost { get; set; } = null!;

    // The welcome DM, so it can be deleted once they confirm. Null when no DM text was configured or
    // the member has DMs closed. The channel id is not stored — NotificationDispatcher re-resolves
    // it, and a stored DM channel is one more thing that can go stale.
    public ulong? DmMessageId { get; set; }

    public BoardingStatus Status { get; set; }

    public DateTimeOffset BoardedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
}
