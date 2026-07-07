namespace HoshiBot.Domain.Entities;

// Holds a modal submission's raw field values when validation fails, so a "Zurück"
// button can reopen the same modal pre-filled instead of making the user retype
// everything from scratch. Referenced by ID from a button's custom-id (same "save
// first, act on the saved row by ID" idea already used for Absence drafts) rather than
// round-tripping the values through the custom-id itself — sidesteps Discord's
// 100-character cap and any ambiguity from freeform text containing the ':' delimiter
// used elsewhere in custom-ids.
public class PendingModalInput
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong DiscordUserId { get; set; }

    public PendingModalInputKind Kind { get; set; }

    // Field meaning depends on Kind:
    //   ShieldReminder: Field1=duration, Field2=system
    //   RaidReport:     Field1=targetUserId, Field2=location, Field3=system, Field4=attacker
    public string? Field1 { get; set; }

    public string? Field2 { get; set; }

    public string? Field3 { get; set; }

    public string? Field4 { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
