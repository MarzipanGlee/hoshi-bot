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
}
