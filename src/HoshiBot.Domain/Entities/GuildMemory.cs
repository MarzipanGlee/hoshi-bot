using Pgvector;

namespace HoshiBot.Domain.Entities;

// One thing Hoshi remembers — a short, distilled statement she formed from chat/conversations and
// can recall later. The evolving counterpart to the static GuildMemberNote lore: memories are
// auto-formed by the consolidation job, ranked by relevance × salience × recency at recall, decay
// when old and unimportant, and are editable/forgettable by staff. Designed for all three memory
// phases via Scope (episodic events now; conversation summaries and per-member memories later).
public class GuildMemory
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public MemoryScope Scope { get; set; }

    // The remembered statement itself, kept short (one/two sentences).
    public string Content { get; set; } = "";

    // How important/durable this memory is (1–5, model-assigned on formation; bumped on recall and
    // when staff pin it). Drives ranking and decay.
    public int Salience { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Bumped whenever the memory is actually retrieved into a prompt — reinforcement, so genuinely
    // useful memories survive decay while never-recalled ones fade.
    public DateTimeOffset? LastRecalledAt { get; set; }

    // Member scope: who the memory is about. PersonKey consolidates a person's alt accounts (reuses
    // MemberNoteService.GetPersonKeysAsync); DiscordUserId is the concrete account it came from.
    public ulong? SubjectDiscordUserId { get; set; }

    public string? SubjectPersonKey { get; set; }

    // Conversation scope: the channel the summarised conversation belongs to.
    public ulong? ChannelId { get; set; }

    // Where the memory was formed from (a channel), for provenance/debugging.
    public ulong? SourceChannelId { get; set; }

    // Semantic embedding of Content (pgvector vector(768), embeddinggemma) — powers similarity search
    // and dedup. Null until embedded.
    public Vector? Embedding { get; set; }

    public string? EmbeddingModel { get; set; }
}
