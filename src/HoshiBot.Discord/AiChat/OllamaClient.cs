using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// The local Ollama backend (IAiChatProvider). Talks to the shared Ollama server whose base URL is
// deployment config (Ollama:BaseUrl) — not a per-guild secret — so no API key is needed. Uses the
// named HttpClient registered in Program.cs (base address + a generous timeout, local generation
// is slow).
//
// Mirrors GeminiClient's contract: never throws for an API/network error — logs at Warning (with
// the response body/status so "model not pulled" / connection-refused is diagnosable straight from
// the logs) and returns null so the caller stays silent.
public class OllamaClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OllamaClient> logger) : IAiChatProvider
{
    public AiProvider Kind => AiProvider.Ollama;

    // The default model lives here (the chat interface), sourced from config so it tracks whatever
    // model is actually pulled in the container — not duplicated as a settings-key constant. The
    // Web editor doesn't show a concrete name (the model is a server-side choice, not per-guild).
    public string DefaultModel =>
        configuration["Ollama:DefaultModel"] is { Length: > 0 } m ? m : "llama3.1:8b";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Cap the context window: our prompt (system instruction + knowledge snippets + short history)
    // fits comfortably in 4k tokens, and a smaller window means a smaller KV cache (less RAM) and
    // faster prompt-eval — the models themselves default to a far larger window (up to 128k) that
    // we neither need nor want to pay for on CPU-only hardware.
    private const int NumCtx = 4096;

    public async Task<string?> GenerateAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        // Ollama's /api/chat takes the system prompt as a leading system-role message, followed by
        // the conversation turns mapped to user/assistant.
        var messages = new List<OllamaMessage>(request.Turns.Count + 1)
        {
            new("system", request.SystemInstruction),
        };
        foreach (var turn in request.Turns)
            messages.Add(new(turn.Role == AiChatRole.Assistant ? "assistant" : "user", turn.Text));

        var body = new OllamaChatRequest(request.Model, messages, Stream: false, Options: new OllamaOptions(NumCtx));

        try
        {
            var http = httpClientFactory.CreateClient(nameof(OllamaClient));
            using var response = await http.PostAsJsonAsync("api/chat", body, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Ollama request failed (model {Model}, status {Status}): {Error}",
                    request.Model, (int)response.StatusCode, errorBody);
                return null;
            }

            var parsed = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
            var text = parsed?.Message?.Content;
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();

            logger.LogWarning("Ollama returned no text (model {Model}, done_reason {DoneReason})", request.Model, parsed?.DoneReason);
            return null;
        }
        // Swallow every provider-side failure — connection refused (server down), a malformed
        // response, and crucially the HttpClient *timeout*, which surfaces as a
        // TaskCanceledException (a subclass of OperationCanceledException). Only a genuine
        // *caller* cancellation (our own token tripped) is allowed to propagate. This is the line
        // to check when an Ollama guild only ever gives the "can't answer" fallback.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ollama request failed (model {Model}): {Error}", request.Model, ex.Message);
            return null;
        }
    }

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("num_ctx")] int NumCtx);

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaOptions Options);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("done_reason")] string? DoneReason);
}
