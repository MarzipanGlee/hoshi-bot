namespace HoshiBot.Domain.Entities;

// A member the automated player-assignment matcher (PlayerLink feature) could NOT confidently link
// to an StfcPlayer — the work item behind the Web admin's "missing assignments" table. Confident
// matches (a single in-alliance roster name match on the member's nickname) are linked silently and
// never produce a row here. The optional, opt-in MemberOnboarding feature also acts on Unresolved
// rows by DMing the member. One row per member per guild; the matcher dedups on (GuildId, DiscordUserId).
public class PlayerLinkReview
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // The linked alliance (GuildAlliance.Id) whose roster we matched against — resolves the roster
    // for the admin picker and the per-alliance PlayerLink/MemberOnboarding settings.
    public int GuildAllianceId { get; set; }

    public ulong DiscordUserId { get; set; }

    // The member's tag-stripped display name at match time (what we tried to match on) — a snapshot
    // so the admin table reads naturally without a live Discord fetch per row.
    public required string Nickname { get; set; }

    public PlayerLinkReviewStatus Status { get; set; }

    // The single best-guess player, if the matcher found exactly one (out-of-alliance) candidate —
    // pre-selects the admin picker / seeds the confirmation DM. Null when there were 0 or >1 matches.
    public int? CandidateStfcPlayerId { get; set; }

    public StfcPlayer? CandidateStfcPlayer { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
