namespace HoshiBot.Domain.Entities;

// One member confirming they have read one post. Find-or-create on the button makes a double click a
// no-op, via a real unique constraint rather than legacy's per-user KV scan.
public class ReadReceipt
{
    public int Id { get; set; }

    public int ReadablePostId { get; set; }

    public ReadablePost ReadablePost { get; set; } = null!;

    public ulong GuildId { get; set; }

    public ulong DiscordUserId { get; set; }

    public GuildMember GuildMember { get; set; } = null!;

    public DateTimeOffset ReadAt { get; set; }
}
