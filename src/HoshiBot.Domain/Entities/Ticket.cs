namespace HoshiBot.Domain.Entities;

// A support thread under the guild's (per-audience) Tickets channel — see
// GuildFeatureSettingsService. No TicketMessage entity — unlike the original sketch,
// there's nothing to mirror: Discord's own thread holds the full conversation, and closing
// never deletes it (archive + lock only, no transcript).
public class Ticket
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ThreadId { get; set; }

    public required string Subject { get; set; }

    public ulong OpenedByDiscordUserId { get; set; }

    // Which audience this ticket was opened for — resolved at open time from which hub
    // button the member clicked (see CommandBridgeButtonModule). Audit only.
    public GuildAudience Audience { get; set; }

    public TicketStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public ulong? ClosedByDiscordUserId { get; set; }
}
