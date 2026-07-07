namespace HoshiBot.Domain.Entities;

// A member's self-set shield-expiration countdown. One per (GuildId, DiscordUserId).
public class ShieldReminder
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public ulong DiscordUserId { get; set; }

    public GuildMember GuildMember { get; set; } = null!;

    public DateTimeOffset ShieldExpiration { get; set; }

    public int? StfcSystemId { get; set; }

    public StfcSystem? StfcSystem { get; set; }

    public bool Disabled { get; set; }

    public ICollection<ShieldReminderNotification> Notifications { get; set; } = [];
}
