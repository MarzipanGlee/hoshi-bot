using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Pgvector;

namespace HoshiBot.Discord.AiChat;

// Resolves and calls this guild's embedding backend: local Ollama (deployment-wide default,
// today's/legacy behavior) or Google Gemini (gemini-embedding-001/gemini-embedding-2, per-guild
// opt-in via AiChatSettingKeys.EmbeddingProvider, reusing the guild's existing chat API key). Thin
// facade over IAiEmbeddingProvider so callers (AiChatIndexService, AiChatService,
// MemoryConsolidationJob, MemberInterviewExtractionJob) keep calling EmbedAsync/EmbedBatchAsync
// exactly as before, just with a guildId added — all resolution happens here.
//
// A guild that has never touched the new setting resolves to exactly today's behavior: Ollama +
// Ollama:EmbeddingModel (or disabled if that's blanked) — see ResolveAsync's default branch. This is
// deliberate: an unrecognized/unset value must never resolve to Gemini, so a typo or stale value
// can't silently start billing a guild's API key.
public class AiChatEmbeddingService(
    IEnumerable<IAiEmbeddingProvider> providers,
    GuildFeatureSettingsService settingsService,
    IConfiguration configuration)
{
    public const string DefaultOllamaModel = "embeddinggemma";

    // Must match the vector(N) column dimension in AiChatIndexedMessageConfiguration /
    // GuildMemoryConfiguration. Both providers are required to honor this — Ollama's embeddinggemma
    // is natively 768-dim; Gemini truncates its native (larger) output via OutputDimensionality.
    public const int Dimensions = 768;

    private const GuildAudience SettingsScope = GuildAudience.None;

    private readonly record struct Resolved(IAiEmbeddingProvider Provider, string Model, string? ApiKey, bool Enabled);

    // Resolves this guild's effective (provider, model, apiKey, enabled). Unset/"ollama"/any
    // unrecognized value -> Ollama + deployment config (today's behavior, unconditionally).
    // "gemini-embedding-001"/"gemini-embedding-2" -> Gemini + that literal model name, using the
    // guild's existing AiChatSettingKeys.ApiKey (the same key already used for chat); enabled only
    // if that key is configured.
    private async Task<Resolved> ResolveAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(
            guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.EmbeddingProvider);

        if (configured is { } value
            && (string.Equals(value, "gemini-embedding-001", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "gemini-embedding-2", StringComparison.OrdinalIgnoreCase)))
        {
            var apiKey = await settingsService.GetSecretAsync(
                guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.ApiKey);
            var geminiProvider = providers.First(p => p.Kind == EmbeddingProvider.Gemini);
            return new Resolved(geminiProvider, value.Trim().ToLowerInvariant(), apiKey, Enabled: !string.IsNullOrWhiteSpace(apiKey));
        }

        var ollamaModel = configuration["Ollama:EmbeddingModel"] is { Length: > 0 } m ? m : DefaultOllamaModel;
        var ollamaProvider = providers.First(p => p.Kind == EmbeddingProvider.Ollama);
        return new Resolved(ollamaProvider, ollamaModel, ApiKey: null,
            Enabled: !string.IsNullOrWhiteSpace(configuration["Ollama:EmbeddingModel"] ?? DefaultOllamaModel));
    }

    // Whether semantic search/recall is available for this guild right now (false => callers
    // degrade to FTS-only / no-recall). False for Ollama when Ollama:EmbeddingModel is explicitly
    // blanked (deployment opt-out, unchanged from today); false for Gemini when no API key is
    // configured yet.
    public async Task<bool> IsEnabledAsync(ulong guildId) => (await ResolveAsync(guildId)).Enabled;

    // The model name currently in effect for this guild — used only to stamp
    // AiChatIndexedMessage.EmbeddingModel / GuildMemory.EmbeddingModel so the existing stale-model
    // re-embed queries (and the cross-model search-filter hardening) keep working.
    public async Task<string> GetModelAsync(ulong guildId) => (await ResolveAsync(guildId)).Model;

    public async Task<Vector?> EmbedAsync(ulong guildId, string text, CancellationToken cancellationToken)
    {
        var results = await EmbedBatchAsync(guildId, [text], cancellationToken);
        return results.Count > 0 ? results[0] : null;
    }

    public async Task<IReadOnlyList<Vector?>> EmbedBatchAsync(ulong guildId, IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
            return [];

        var resolved = await ResolveAsync(guildId);
        if (!resolved.Enabled)
            return new Vector?[texts.Count];

        return await resolved.Provider.EmbedBatchAsync(resolved.Model, resolved.ApiKey, texts, cancellationToken);
    }
}
