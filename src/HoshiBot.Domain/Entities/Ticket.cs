namespace HoshiBot.Domain.Entities;

// A support thread under GuildSettings.TicketsChannelId. No TicketMessage entity — unlike
// the original sketch, there's nothing to mirror: Discord's own thread holds the full
// conversation, and closing never deletes it (archive + lock only, no transcript).
public class Ticket
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ThreadId { get; set; }

    public required string Subject { get; set; }

    public ulong OpenedByDiscordUserId { get; set; }

    public TicketStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public ulong? ClosedByDiscordUserId { get; set; }
}
