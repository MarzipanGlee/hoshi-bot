namespace HoshiBot.Domain.Entities;

public class ThreadRemovalRequest
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ThreadId { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public ulong RequestedByDiscordUserId { get; set; }
}
