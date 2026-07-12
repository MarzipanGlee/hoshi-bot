namespace HoshiBot.Domain.Entities;

// One row per Alliance Tournament / Infinite Incursions blog post StfcNewsNotifyJob has
// detected on the official STFC WordPress feed — also the single shared "draft" for its
// crowd-sourced date-confirmation lifecycle (detection and date-entry are about the same
// event, so one row tracks both rather than a separate draft table).
public class StfcNewsPost
{
    public int Id { get; set; }

    // The RSS <link> permalink (e.g. startrekfleetcommand.com/news/[slug]/) — stable per
    // WordPress post, used as the dedupe key for detection.
    public required string Link { get; set; }

    public required string Title { get; set; }

    // Best-effort parse of the RSS <pubDate>; nullable because a third-party feed's date
    // format is out of our control — a parse failure must never block dedup/notification,
    // just leave this null. Diagnostic only, not used in any date math.
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset DetectedAt { get; set; }

    // Which StfcEventStatus.EventGroup this feeds ("incursions" or "alliance_tournaments"),
    // decided at detection time by which title keyword matched.
    public required string EventGroup { get; set; }

    // Crowd-sourced submission state — ONE shared candidate DATE at a time across every
    // guild pinged for this post (region-specific times, for Incursions, are derived
    // separately from IncursionsRegionDefault, not submitted here). Resubmitting via Edit
    // overwrites these and clears Confirmations — a new date needs a fresh quorum.
    public DateOnly? SubmittedDate { get; set; }

    public ulong? SubmittedByDiscordUserId { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    // Computed once, right after all guild messages are sent, from
    // StfcNewsSettings.RequiredConfirmationPercentage applied to the summed
    // EligibleMemberCount across every StfcNewsPostGuildMessage row for this post. Stored
    // (not recomputed later) so the target doesn't drift if guild membership changes
    // mid-flight. Always at least 1.
    public int RequiredConfirmations { get; set; }

    // Cache of the confirmation count last actually displayed in the guild messages — lets
    // the batched stats refresh (StfcNewsStatsRefreshJob) skip a Discord edit call when
    // nothing's changed, same idea as Announcement.LastKnownReadCount.
    public int LastDisplayedConfirmationCount { get; set; }

    // Set once resolved (quorum reached, or a trusted user confirmed) and StfcEventStatus
    // has been updated; after this, Enter Date/Edit/Confirm are inert everywhere.
    public DateTimeOffset? ResolvedAt { get; set; }

    public List<StfcNewsPostGuildMessage> GuildMessages { get; set; } = [];

    public List<StfcEventDateConfirmation> Confirmations { get; set; } = [];
}
