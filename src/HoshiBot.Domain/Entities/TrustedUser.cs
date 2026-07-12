namespace HoshiBot.Domain.Entities;

// Bot-wide (not per-guild) list of Discord users whose confirmation of an StfcNewsPost's
// submitted event date is trusted enough to resolve it immediately, without waiting for the
// usual confirmation quorum. Separate from GlobalAdmin — a trusted user isn't necessarily a
// bot administrator, just someone whose word on an event date is taken as final.
public class TrustedUser
{
    public int Id { get; set; }

    public required ulong DiscordUserId { get; set; }

    // Optional, for admin-UI readability only — never used for lookups.
    public string? DisplayName { get; set; }

    public DateTimeOffset AddedAt { get; set; }
}
