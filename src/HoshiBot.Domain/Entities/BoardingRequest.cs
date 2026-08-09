namespace HoshiBot.Domain.Entities;

// A job the Web admin asked the bot to do: publish the standing message, or board the members who
// were already here when the feature was switched on.
//
// A queue row rather than a setting flag because HoshiBot.Web must never reference HoshiBot.Discord
// — and because the admin needs to see whether it worked. Mirrors CommandBridgeRepublishRequest
// exactly, including the attempt/error columns the editor polls: the row vanishing means success,
// LastError means show it.
public class BoardingRequest
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildAudience Audience { get; set; }

    public int? GuildAllianceId { get; set; }

    public GuildAlliance? GuildAlliance { get; set; }

    public BoardingRequestKind Kind { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public string? LastError { get; set; }
}

public enum BoardingRequestKind
{
    // Post the standing message, or edit the one already there.
    Publish,

    // Board everyone already in the guild who qualifies, ignoring the EnabledAt cutoff. Silent: the
    // backfill never DMs, however the welcome text is configured.
    Backfill,
}
