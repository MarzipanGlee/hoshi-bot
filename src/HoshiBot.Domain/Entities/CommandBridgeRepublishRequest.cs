namespace HoshiBot.Domain.Entities;

// A queued request (written by the Web admin panel) to (re)post a guild's Command Bridge hub
// message for one bridge. The bot Host drains these in CommandBridgeRepublishJob and calls
// the hub service — this indirection keeps all Discord message-building in HoshiBot.Discord,
// which HoshiBot.Web must not reference. Mirrors ThreadRemovalRequest's queue shape.
public class CommandBridgeRepublishRequest
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public CommandBridgeKind Bridge { get; set; }

    public DateTimeOffset RequestedAt { get; set; }
}
