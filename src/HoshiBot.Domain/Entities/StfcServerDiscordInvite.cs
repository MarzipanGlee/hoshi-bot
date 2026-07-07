namespace HoshiBot.Domain.Entities;

// A server can have more than one active Discord invite link (e.g. a backup server),
// hence a dedicated child table rather than a single nullable Url column on StfcServer.
public class StfcServerDiscordInvite
{
    public int Id { get; set; }

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public required string Url { get; set; }
}
