namespace HoshiBot.Domain.Entities;

// Diplomacy is an in-game STFC fact, not a guild opinion: one alliance sets a
// diplomatic status toward another, independent of which Discord guild is looking.
// Absence of a row means no status has been set. A guild's stance toward another
// alliance (via /set-diplomacy) is just the row where SourceAllianceId is one of the
// guild's GuildAlliance-linked alliances.
// Uniqueness on (SourceAllianceId, TargetAllianceId) enforced in application code.
public class StfcAllianceDiplomacy
{
    public int Id { get; set; }

    public int SourceAllianceId { get; set; }

    public StfcAlliance SourceAlliance { get; set; } = null!;

    public int TargetAllianceId { get; set; }

    public StfcAlliance TargetAlliance { get; set; } = null!;

    public DiplomacyStatus Status { get; set; }
}
