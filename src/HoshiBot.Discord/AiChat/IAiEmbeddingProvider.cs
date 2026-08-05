using HoshiBot.Domain.Entities;
using Pgvector;

namespace HoshiBot.Discord.AiChat;

// Which embedding backend produces the vector leg of hybrid knowledge search and episodic/member
// memory recall for a guild. Independent of AiProvider (which selects the *chat* backend) — a
// guild's embedding backend is resolved separately via AiBackendSettingKeys.EmbeddingProvider. One
// enum value per implementation class, same convention as AiProvider: Gemini's two embedding
// models ("gemini-embedding-001", "gemini-embedding-2") are just different `model` strings passed
// into the same GeminiEmbeddingProvider, mirroring how AiProvider.Gemini already carries a
// separately-configurable chat Model string.
public enum EmbeddingProvider
{
    Ollama,
    Gemini,
}

// The outcome of one embedding batch: one vector per input (same order; an entry is null only when
// the whole call failed) plus an optional Error message on failure. Error carries the provider's
// own message (e.g. a quota/billing string) so callers can surface it for health/observability;
// null on success. Mirrors IAiChatProvider's AiChatGeneration carrying a failure classification.
public readonly record struct EmbeddingBatchResult(IReadOnlyList<Vector?> Vectors, string? Error);

// A swappable embedding backend. Implementations must never throw for an API/network error — they
// log and return an all-null result (with Error set) so callers degrade to FTS-only / no-recall,
// mirroring IAiChatProvider's never-throw contract.
public interface IAiEmbeddingProvider
{
    EmbeddingProvider Kind { get; }

    // Embeds a batch of texts in one call. Returns one entry per input, same order; entries are null
    // only if the whole call failed, in which case Error carries the provider message (never throws —
    // logs and returns all-null + Error on total failure). `model` is the concrete model name to call
    // (e.g. "embeddinggemma", "gemini-embedding-001", "gemini-embedding-2"). `apiKey` is the guild's
    // decrypted Gemini key; ignored by Ollama (kept in the signature so the facade can call either
    // provider polymorphically with no Kind-branch at the call site).
    Task<EmbeddingBatchResult> EmbedBatchAsync(
        string model, string? apiKey, IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
