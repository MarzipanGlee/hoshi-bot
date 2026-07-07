namespace HoshiBot.Domain.Entities;

// Find-or-create on the read-confirm button makes double-clicking a no-op, matching
// legacy's idempotent per-user confirmation dict — but via a real unique constraint
// instead of a per-user KV scan.
public class AnnouncementReadReceipt
{
    public int Id { get; set; }

    public int AnnouncementId { get; set; }

    public Announcement Announcement { get; set; } = null!;

    public ulong GuildId { get; set; }

    public ulong DiscordUserId { get; set; }

    public GuildMember GuildMember { get; set; } = null!;

    public DateTimeOffset ReadAt { get; set; }
}
