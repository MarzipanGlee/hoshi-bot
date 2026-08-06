using System.Collections.Concurrent;

namespace HoshiBot.Domain.Entities;

// Stops the bot reissuing a Discord call that just failed.
//
// Every repeat-forever loop in this codebase has the same shape: a catch inside a per-item loop, no
// state advancing on failure, so the next run makes the identical call. Command Bridge republish
// runs every 5 seconds, which is 240 invalid requests per 10 minutes from a single stuck row —
// against Discord's ban threshold of 10,000 per 10 minutes.
//
// Deliberately reactive rather than a permission pre-check: this covers 429s, 5xx, deleted channels
// and permission changes alike, and because it only ever acts AFTER a real failure it cannot
// wrongly suppress a call that would have succeeded. (Pre-checking is not forbidden — see
// CONTRIBUTING "Discord API limits" — it just earns little once this is in place.)
//
// Mirrors NotificationDispatcher's admin-notify throttle on purpose: same ladder shape, same
// process-lifetime in-memory state, same clear-on-recovery. They are not the same thing though —
// that one throttles the ADMIN MESSAGE, this one throttles the DISCORD CALL.
public sealed class ChannelCooldown
{
    // Capped on purpose. A cooldown that grew without bound would become a permanent silent block,
    // which is exactly the failure mode this is supposed to prevent: something that stops working
    // and never tells anyone. Half an hour is long enough to be negligible against any job cycle
    // here, short enough that a fixed channel recovers on its own.
    private static readonly TimeSpan[] Ladder =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
    ];

    // Channel ids are globally unique snowflakes, so the guild isn't needed to key this. BotAction
    // separates operations on the same channel — a failing thread delete shouldn't silence an alert
    // post in the same place.
    private readonly ConcurrentDictionary<(ulong ChannelId, BotAction Action), (DateTimeOffset FailedAt, int Failures)> _state = new();

    public bool IsCoolingDown(ulong channelId, BotAction action) =>
        IsCoolingDown(channelId, action, DateTimeOffset.UtcNow);

    public bool IsCoolingDown(ulong channelId, BotAction action, DateTimeOffset now) =>
        _state.TryGetValue((channelId, action), out var state)
        && now - state.FailedAt < Wait(state.Failures);

    public void RecordFailure(ulong channelId, BotAction action) =>
        RecordFailure(channelId, action, DateTimeOffset.UtcNow);

    public void RecordFailure(ulong channelId, BotAction action, DateTimeOffset now) =>
        _state.AddOrUpdate((channelId, action), (now, 1), (_, previous) => (now, previous.Failures + 1));

    // Called on the way out of a successful call, so a channel that starts working again is back to
    // full speed immediately rather than serving out a cooldown it no longer deserves.
    public void RecordSuccess(ulong channelId, BotAction action) =>
        _state.TryRemove((channelId, action), out _);

    // Public so callers whose retry state is a DB row rather than this dictionary use the same
    // ladder — CommandBridgeRepublishJob keeps its attempt count on the queued row, which survives
    // a restart, and that matters more there than anywhere else: the job runs every five seconds.
    public static TimeSpan WaitAfter(int failures) => Ladder[Math.Min(Math.Max(failures, 1) - 1, Ladder.Length - 1)];

    private static TimeSpan Wait(int failures) => WaitAfter(failures);
}
