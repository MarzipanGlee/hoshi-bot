namespace HoshiBot.Domain.Entities;

// Which class of AI backend call a health row tracks — the call's purpose, not the provider brand
// (the guild's Gemini/Ollama choice lives in the AiBackend feature settings).
public enum AiChatProviderCallKind
{
    // Chat completions (the answer/gate/router generations).
    Chat,

    // Embedding generation (the vector leg of hybrid retrieval + memory recall).
    Embed,
}

// The latest observed health of a guild's AI backend, one row per (guild, call kind). Written by the
// bot from its own provider calls — a success stamps LastSuccessAt, a failure stamps LastErrorAt +
// LastErrorMessage — so an operator can see a quota/billing/outage state (e.g. the silent 24h Gemini
// embedding-quota degradation) from the Web admin instead of grepping bot logs + querying Postgres.
// Read-only in the UI; the bot is the only writer.
public class AiChatProviderHealth
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public AiChatProviderCallKind Kind { get; set; }

    // The last time a call of this kind succeeded / failed for the guild (either may be null before
    // the first observation of that outcome).
    public DateTimeOffset? LastSuccessAt { get; set; }

    public DateTimeOffset? LastErrorAt { get; set; }

    // Short error text from the most recent failure (truncated on write) — e.g. the provider's
    // quota/billing message. Null when no failure has been recorded.
    public string? LastErrorMessage { get; set; }

    // The model in effect at the last recorded outcome, for context.
    public string? Model { get; set; }
}
