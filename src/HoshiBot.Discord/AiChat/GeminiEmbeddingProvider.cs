using System.Collections.Concurrent;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace HoshiBot.Discord.AiChat;

// The Google Gemini embedding backend (IAiEmbeddingProvider). Mirrors GeminiClient's (chat)
// Client-caching-per-API-key + hard request timeout + never-throw pattern — see that file for the
// full rationale. Serves both "gemini-embedding-001" (text-only) and "gemini-embedding-2"
// (multimodal-capable) — they differ only by model name here; wiring non-text input (image/video/
// audio/PDF) through the knowledge-indexing pipeline is a separate, not-yet-built feature (see
// docs/backlog.md), so only the text overload is used regardless of which model a guild picks.
public class GeminiEmbeddingProvider(ILogger<GeminiEmbeddingProvider> logger) : IAiEmbeddingProvider
{
    public EmbeddingProvider Kind => EmbeddingProvider.Gemini;

    // Own cache, not shared with GeminiClient's — Client is cheap to key off the API key string, and
    // each provider class owns the cache it uses so this file has no compile-time dependency on
    // GeminiClient.
    private static readonly ConcurrentDictionary<string, Client> ClientsByApiKey = new();

    // Same 30s cap as GeminiClient's chat calls, for the same reason: fail fast into the null/
    // degrade-to-FTS-only path rather than hang an index/query-time embed call.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public async Task<EmbeddingBatchResult> EmbedBatchAsync(
        string model, string? apiKey, IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
            return new EmbeddingBatchResult([], null);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            const string message = "Gemini embedding requested without an API key; degrading to FTS-only.";
            logger.LogWarning(message);
            return new EmbeddingBatchResult(new Vector?[texts.Count], message);
        }

        var client = ClientsByApiKey.GetOrAdd(apiKey, key => new Client(apiKey: key));

        // Truncates Gemini's native (larger) output down to the fixed vector(768) column width —
        // see AiChatEmbeddingService.Dimensions. Supported by both embedding models used here.
        var config = new EmbedContentConfig { OutputDimensionality = AiChatEmbeddingService.Dimensions };
        var contents = texts.Select(t => new Content { Parts = [new Part { Text = t }] }).ToList();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            var response = await client.Models.EmbedContentAsync(model, contents, config, timeoutCts.Token);

            var embeddings = response?.Embeddings;
            if (embeddings is null || embeddings.Count != texts.Count)
            {
                var message = $"Gemini embed returned {embeddings?.Count.ToString() ?? "null"} vectors for {texts.Count} inputs (model {model})";
                logger.LogWarning("{Message}", message);
                return new EmbeddingBatchResult(new Vector?[texts.Count], message);
            }

            var result = new Vector?[texts.Count];
            for (var i = 0; i < texts.Count; i++)
                result[i] = embeddings[i].Values is { Count: > 0 } values
                    ? new Vector(values.Select(v => (float)v).ToArray())
                    : null;
            return new EmbeddingBatchResult(result, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Gemini embed failed (model {Model}, {Count} inputs): {Error}", model, texts.Count, ex.Message);
            return new EmbeddingBatchResult(new Vector?[texts.Count], ex.Message);
        }
    }
}
