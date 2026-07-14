namespace HoshiBot.Domain.Entities;

// A private thread under the guild's RoeViolationReports channel (see
// GuildFeatureSettingsService), one per reported RoE violation. Attacker/defender
// tag+name are stored as plain strings — the "other side"
// (the party not identified via a real Discord user-select) may not even be a member of
// this guild, matching legacy exactly. Closing archives+locks the thread in place, same
// as Ticket — no transcript needed.
public class RoeViolationReport
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ThreadId { get; set; }

    public required string AttackerAllianceTag { get; set; }

    public required string AttackerCommanderName { get; set; }

    public required string DefenderAllianceTag { get; set; }

    public required string DefenderCommanderName { get; set; }

    public ulong ReportedByDiscordUserId { get; set; }

    public RoeViolationStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public ulong? ClosedByDiscordUserId { get; set; }

    // Which linked alliance this report belongs to (the reporter's alliance) — determines which
    // alliance's RoE channel it was posted to and whose diplomat role gets pinged. Nullable and
    // SetNull on unlink: a historical report outlives the link. Null for reports created before
    // per-alliance scoping, or by a member with no resolvable alliance (fell back to primary).
    public int? GuildAllianceId { get; set; }

    public GuildAlliance? GuildAlliance { get; set; }
}
