namespace HoshiBot.Domain.Entities;

// The first bot-wide (not per-guild) *tunable setting* entity in this app — a fixed single
// row (Id = 1). Deliberately holds two related-but-distinct knobs (confirmation quorum size,
// and Infinite Incursions' event duration) rather than introducing a second tiny singleton
// entity just for one field.
public class StfcNewsSettings
{
    public int Id { get; set; }

    // Percentage (not a flat count) of the summed StfcNewsPostGuildMessage.EligibleMemberCount
    // across every guild pinged for a post, required before a submitted date auto-resolves.
    public int RequiredConfirmationPercentage { get; set; } = 20;

    // Infinite Incursions' event length in hours — currently 12, was 24 in the past, so kept
    // editable rather than hardcoded. Used as EventEnd = EventStart + this, per region.
    public int IncursionsEventDurationHours { get; set; } = 12;
}
