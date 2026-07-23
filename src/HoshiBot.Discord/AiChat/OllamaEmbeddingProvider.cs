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

    public async Task<IReadOnlyList<Vector?>> EmbedBatchAsync(
        string model, string? apiKey, IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
            return [];

        try
        {
            var client = CreateClient();
            var response = await client.EmbedAsync(new EmbedRequest { Model = model, Input = [.. texts] }, cancellationToken);

            var embeddings = response?.Embeddings;
            if (embeddings is null || embeddings.Count != texts.Count)
            {
                logger.LogWarning(
                    "Ollama embed returned {Got} vectors for {Expected} inputs (model {Model})",
                    embeddings?.Count.ToString() ?? "null", texts.Count, model);
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
            logger.LogWarning(ex, "Ollama embed failed (model {Model}, {Count} inputs): {Error}", model, texts.Count, ex.Message);
            return new Vector?[texts.Count];
        }
    }
}
