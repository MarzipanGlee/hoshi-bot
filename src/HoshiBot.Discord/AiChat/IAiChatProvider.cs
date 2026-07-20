namespace HoshiBot.Discord.AiChat;

// The LLM backends the AI-chat feature can answer with. A guild picks one via the per-guild
// AiChatSettingKeys.Provider setting; AiChatService resolves the matching IAiChatProvider.
public enum AiProvider
{
    Gemini,
    Ollama,
}

// Neutral conversation-turn role. Each provider maps it to its own wire name (Gemini calls the
// bot's turns "model", Ollama calls them "assistant") — callers never touch provider-specific
// role strings.
public enum AiChatRole
{
    User,
    Assistant,
}

// One conversation turn passed to a provider.
public readonly record struct AiChatTurn(AiChatRole Role, string Text);

// Everything a provider needs for a single generation. ApiKey is only meaningful for providers
// that authenticate per guild (Gemini); the shared local Ollama ignores it.
public sealed record AiChatCompletionRequest(
    string Model,
    string SystemInstruction,
    IReadOnlyList<AiChatTurn> Turns,
    string? ApiKey);

// Why a generation produced no text — lets callers pick a friendlier reply for a transient provider
// hiccup (overload / timeout / "high demand") than for a genuinely empty or blocked response.
public enum AiChatFailureKind
{
    None,        // there is text
    Overloaded,  // transient: timeout, model overloaded, "high demand", 429/503 — worth a "busy, try again" reply
    Other,       // empty / safety-blocked / config error — an honest "can't answer" fits better
}

// A generation result plus, on a null Text, why it failed. GenerateAsync returns just the text;
// GenerateDetailedAsync adds the classification.
public readonly record struct AiChatGeneration(string? Text, AiChatFailureKind Failure);

// A swappable chat backend. Implementations must never throw for an API/network error — they log
// and return null so AiChatService's "null ⇒ stay silent / politely-unsure" logic is uniform
// across providers.
public interface IAiChatProvider
{
    AiProvider Kind { get; }

    // The model used when a guild hasn't set an explicit Model override.
    string DefaultModel { get; }

    // The small/fast model used for the passive-listening gate pass (a cheap yes/no "is this an
    // answerable question?" classifier that runs before the expensive retrieval + main generation).
    // Null means no default gate for this provider — the gate stays off unless a guild configures
    // AiChatSettingKeys.GateModel. See AiChatService's gate logic. Only ever suppresses on a
    // confident NO; any failure/ambiguity falls through to the main model, so a wrong/absent gate
    // model is safe (it just no-ops).
    string? DefaultGateModel { get; }

    // How much grounding context AiChatService assembles into the prompt for this backend. On
    // CPU-only hardware the prompt-eval of a large prompt dominates latency, so local backends
    // (Ollama) run leaner; cloud backends (Gemini) can afford the fuller window.
    int HistoryLimit { get; }
    int KnowledgeSnippetLimit { get; }

    // Returns the model's text answer, or null on any failure/empty response.
    Task<string?> GenerateAsync(AiChatCompletionRequest request, CancellationToken cancellationToken);

    // Like GenerateAsync but classifies a null result so callers can tell a transient overload/timeout
    // (worth a friendly "busy, try again" reply) from an empty/blocked one. The default can't
    // distinguish — it reports Other on null; a provider that knows better (GeminiClient) overrides it.
    async Task<AiChatGeneration> GenerateDetailedAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var text = await GenerateAsync(request, cancellationToken);
        return new AiChatGeneration(text, text is null ? AiChatFailureKind.Other : AiChatFailureKind.None);
    }

    // Streaming variant: yields the answer incrementally as text *deltas* (each item is the new
    // fragment, not the running total — the caller accumulates). Same never-throw contract as
    // GenerateAsync: on any API/network error it logs and stops yielding, so the caller keeps
    // whatever arrived so far. Used for the live-typing UX on directly-addressed messages.
    IAsyncEnumerable<string> GenerateStreamAsync(AiChatCompletionRequest request, CancellationToken cancellationToken);
}
