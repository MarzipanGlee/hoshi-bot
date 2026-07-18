using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using Pgvector;

namespace HoshiBot.Discord.AiChat;

// Generates text embeddings via the shared local Ollama server (deployment-wide, independent of
// each guild's chat provider) using OllamaSharp's /api/embed. Powers the vector leg of the hybrid
// knowledge search: AiChatIndexService embeds indexed messages (in the backfill job) and the live
// query with the same model, so their vectors are comparable.
//
// Enabled only when Ollama:EmbeddingModel is set (default embeddinggemma, 768 dims to match the
// AiChatIndexedMessage.Embedding column). Blank config ⇒ semantic search is off and retrieval
// stays FTS-only. Never throws for an API/network error — logs and returns null(s) so callers
// degrade to FTS-only.
public class AiChatEmbeddingService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<AiChatEmbeddingService> logger)
{
    public const string DefaultModel = "embeddinggemma";

    // Must match the vector(N) column dimension in AiChatIndexedMessageConfiguration.
    public const int Dimensions = 768;

    public string Model =>
        configuration["Ollama:EmbeddingModel"] is { Length: > 0 } m ? m : DefaultModel;

    // Semantic search is disabled when the model is explicitly blanked in config.
    public bool Enabled => !string.IsNullOrWhiteSpace(configuration["Ollama:EmbeddingModel"] ?? DefaultModel);

    // Reuses the named HttpClient registered for OllamaClient (same base URL + timeout). The
    // OllamaApiClient does not own/dispose an externally supplied HttpClient.
    private OllamaApiClient CreateClient() => new(httpClientFactory.CreateClient(nameof(OllamaClient)));

    // Embeds a single text; null on failure/disabled.
    public async Task<Vector?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var results = await EmbedBatchAsync([text], cancellationToken);
        return results.Count > 0 ? results[0] : null;
    }

    // Embeds a batch in one /api/embed call. Returns one entry per input (same order); an entry is
    // null if the whole call failed. Returns all-null (never throws) on any API/network error.
    public async Task<IReadOnlyList<Vector?>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (!Enabled || texts.Count == 0)
            return new Vector?[texts.Count];

        try
        {
            var client = CreateClient();
            var response = await client.EmbedAsync(new EmbedRequest { Model = Model, Input = [.. texts] }, cancellationToken);

            var embeddings = response?.Embeddings;
            if (embeddings is null || embeddings.Count != texts.Count)
            {
                logger.LogWarning(
                    "Ollama embed returned {Got} vectors for {Expected} inputs (model {Model})",
                    embeddings?.Count.ToString() ?? "null", texts.Count, Model);
                return new Vector?[texts.Count];
            }

            var result = new Vector?[texts.Count];
            for (var i = 0; i < texts.Count; i++)
                result[i] = new Vector(embeddings[i]);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Model not pulled, server down, dimension mismatch, etc. — the line to check when
            // semantic search silently stays FTS-only.
            logger.LogWarning(ex, "Ollama embed failed (model {Model}, {Count} inputs): {Error}", Model, texts.Count, ex.Message);
            return new Vector?[texts.Count];
        }
    }
}
