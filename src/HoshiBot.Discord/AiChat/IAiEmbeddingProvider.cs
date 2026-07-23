using Pgvector;

namespace HoshiBot.Discord.AiChat;

// Which embedding backend produces the vector leg of hybrid knowledge search and episodic/member
// memory recall for a guild. Independent of AiProvider (which selects the *chat* backend) — a
// guild's embedding backend is resolved separately via AiChatSettingKeys.EmbeddingProvider. One
// enum value per implementation class, same convention as AiProvider: Gemini's two embedding
// models ("gemini-embedding-001", "gemini-embedding-2") are just different `model` strings passed
// into the same GeminiEmbeddingProvider, mirroring how AiProvider.Gemini already carries a
// separately-configurable chat Model string.
public enum EmbeddingProvider
{
    Ollama,
    Gemini,
}

// A swappable embedding backend. Implementations must never throw for an API/network error — they
// log and return an all-null result so callers degrade to FTS-only / no-recall, mirroring
// IAiChatProvider's never-throw contract.
public interface IAiEmbeddingProvider
{
    EmbeddingProvider Kind { get; }

    // Embeds a batch of texts in one call. Returns one entry per input, same order; an entry is
    // null only if the whole call failed (never throws — logs and returns all-null on total
    // failure). `model` is the concrete model name to call (e.g. "embeddinggemma",
    // "gemini-embedding-001", "gemini-embedding-2"). `apiKey` is the guild's decrypted Gemini key;
    // ignored by Ollama (kept in the signature so the facade can call either provider
    // polymorphically with no Kind-branch at the call site).
    Task<IReadOnlyList<Vector?>> EmbedBatchAsync(
        string model, string? apiKey, IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
