namespace HoshiBot.Domain.Entities;

// One indexed Discord message from a guild's AI-chat knowledge channels — the persistent search
// index the AI chat feature queries to ground answers, instead of re-fetching recent messages from
// Discord on every question. Populated live (as messages arrive) and by a periodic backfill job.
//
// Content is the rendered text (message content + embed text). There is deliberately no stored
// tsvector column: the search language is per-guild, and a generated tsvector column would require
// a constant text-search config — so full-text matching computes to_tsvector at query time with
// the guild's configured language instead (see AiChatIndexService.SearchAsync).
public class AiChatIndexedMessage
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ChannelId { get; set; }

    // The Discord message id — the natural key the live + backfill paths upsert on.
    public ulong MessageId { get; set; }

    // Denormalized channel name so retrieval can tell the model which channel a snippet came from
    // (the persona points members at the right channel) without a live channel lookup.
    public string? ChannelName { get; set; }

    public string? AuthorName { get; set; }

    public string Content { get; set; } = "";

    // The message's own timestamp (for recency ordering among search matches).
    public DateTimeOffset CreatedAt { get; set; }

    // When this row was last (re)indexed.
    public DateTimeOffset IndexedAt { get; set; }
}
