using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using Pgvector;

namespace HoshiBot.Discord.AiChat;

// The shared local Ollama server as an embedding backend (IAiEmbeddingProvider). Deployment-wide —
// reuses the same named HttpClient OllamaClient (chat) uses; no per-guild API key.
public class OllamaEmbeddingProvider(IHttpClientFactory httpClientFactory, ILogger<OllamaEmbeddingProvider> logger)
    : IAiEmbeddingProvider
{
    public EmbeddingProvider Kind => EmbeddingProvider.Ollama;

    // Reuses the named HttpClient registered for OllamaClient (same base URL + timeout). The
    // OllamaApiClient does not own/dispose an externally supplied HttpClient.
    private OllamaApiClient CreateClient() => new(httpClientFactory.CreateClient(nameof(OllamaClient)));

    public async Task<EmbeddingBatchResult> EmbedBatchAsync(
        string model, string? apiKey, IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
            return new EmbeddingBatchResult([], null);

        try
        {
            var client = CreateClient();
            var response = await client.EmbedAsync(new EmbedRequest { Model = model, Input = [.. texts] }, cancellationToken);

            var embeddings = response?.Embeddings;
            if (embeddings is null || embeddings.Count != texts.Count)
            {
                var message = $"Ollama embed returned {embeddings?.Count.ToString() ?? "null"} vectors for {texts.Count} inputs (model {model})";
                logger.LogWarning("{Message}", message);
                return new EmbeddingBatchResult(new Vector?[texts.Count], message);
            }

            var result = new Vector?[texts.Count];
            for (var i = 0; i < texts.Count; i++)
                result[i] = new Vector(embeddings[i]);
            return new EmbeddingBatchResult(result, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Model not pulled, server down, dimension mismatch, etc. — the line to check when
            // semantic search silently stays FTS-only.
            logger.LogWarning(ex, "Ollama embed failed (model {Model}, {Count} inputs): {Error}", model, texts.Count, ex.Message);
            return new EmbeddingBatchResult(new Vector?[texts.Count], ex.Message);
        }
    }
}
