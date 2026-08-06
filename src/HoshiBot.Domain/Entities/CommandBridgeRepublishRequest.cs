namespace HoshiBot.Domain.Entities;

// A queued request (written by the Web admin panel) to (re)post one linked alliance's Command
// Bridge hub message for one bridge. The bot Host drains these in CommandBridgeRepublishJob and
// calls the hub service — this indirection keeps all Discord message-building in
// HoshiBot.Discord, which HoshiBot.Web must not reference. Mirrors ThreadRemovalRequest's
// queue shape.
public class CommandBridgeRepublishRequest
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // Which linked alliance's bridge to (re)post — Command Bridges are per-alliance.
    public int GuildAllianceId { get; set; }

    public GuildAlliance GuildAlliance { get; set; } = null!;

    public CommandBridgeKind Bridge { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    // Retry state. The job used to leave a failed row queued with nothing recorded, so it retried
    // the same doomed publish every 5 seconds forever — 240 invalid Discord requests per 10 minutes
    // from one stuck row, against a ban threshold of 10,000. These three make the retry back off
    // (ChannelCooldown.WaitAfter), stop after a handful of attempts, and — because the Web publish
    // button polls this row — let the admin be told WHY instead of watching a spinner forever.
    //
    // Kept on the row rather than in memory: this job's schedule is short enough that a restart
    // would otherwise discard the backoff and resume hammering.
    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    // Null while the request is merely queued; set to the failure reason once one has happened.
    public string? LastError { get; set; }
}
