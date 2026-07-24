namespace HoshiBot.Domain.Entities;

// One row per Territory Capture message the bot has posted (Single reminder / Daily digest /
// Weekly digest), tracked so it can be cleaned up on its own schedule — the new bot's TC messages
// used to pile up forever. TerritoryCaptureReminderJob's sweep deletes the Discord message once
// ExpiresAt has passed (deleting a pinned Weekly also unpins it). DedupKey (mirroring legacy's
// ReminderKey — e.g. "single-{linkId}-{territoryId}-{startUnix}", "weekly-{linkId}-{yyyyMMdd}") is
// unique per alliance link, so a re-run or a cron misfire-replay can't post the same message twice.
public class TerritoryCaptureSentMessage
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    // The GuildAlliance link id (not the StfcAlliance id) — settings and slot roles are keyed per
    // link, and DedupKey is scoped to it.
    public int GuildAllianceId { get; set; }

    public TerritoryCaptureMessageKind Kind { get; set; }

    public string DedupKey { get; set; } = null!;

    public ulong ChannelId { get; set; }

    public ulong MessageId { get; set; }

    public DateTimeOffset SentAt { get; set; }

    // When the sweep should delete this message: capture End (Single), SentAt + 1 day (Daily),
    // SentAt + 7 days (Weekly — no capture-free day anymore, so the pin lives the whole week).
    public DateTimeOffset ExpiresAt { get; set; }
}
