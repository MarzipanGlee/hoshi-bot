using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace HoshiBot.Discord.AiChat;

// The local Ollama backend (IAiChatProvider), via OllamaSharp over the shared Ollama server whose
// base URL is deployment config (Ollama:BaseUrl) — not a per-guild secret, so no API key. Uses the
// named HttpClient registered in Program.cs (base address + a generous timeout, local generation
// is slow).
//
// Mirrors GeminiClient's contract: never throws for an API/network error — logs at Warning (so
// "model not pulled" / connection-refused is diagnosable straight from the logs) and returns null
// so the caller stays silent.
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

    // Ollama gate is opt-in: the small classifier model must be pulled first (docker compose exec
    // ollama ollama pull <model>), so there's no baked-in default — the gate stays off until a
    // deployment sets Ollama:GateModel (or a guild sets AiBackendSettingKeys.GateModel). Null keeps the
    // current behaviour (main model decides via [NO_ANSWER]).
    public string? DefaultGateModel =>
        configuration["Ollama:GateModel"] is { Length: > 0 } m ? m : null;

    // Local/CPU backend: prompt-eval of a big prompt is the dominant latency cost, so run lean.
    // Tunable via config for deployments with more (or less) CPU headroom.
    public int HistoryLimit => configuration.GetValue<int?>("Ollama:HistoryLimit") ?? 8;
    public int KnowledgeSnippetLimit => configuration.GetValue<int?>("Ollama:KnowledgeSnippetLimit") ?? 6;

    // Cap the context window: our prompt (system instruction + knowledge snippets + short history)
    // fits comfortably in 4k tokens, and a smaller window means a smaller KV cache (less RAM) and
    // faster prompt-eval — the models themselves default to a far larger window (up to 128k) that
    // we neither need nor want to pay for on CPU-only hardware.
    private const int NumCtx = 4096;

    // The system prompt is a leading system-role message, then the conversation turns mapped to
    // user/assistant.
    private static ChatRequest BuildChatRequest(AiChatCompletionRequest request, bool stream)
    {
        var messages = new List<Message>(request.Turns.Count + 1)
        {
            new(ChatRole.System, request.SystemInstruction),
        };
        foreach (var turn in request.Turns)
            messages.Add(new(turn.Role == AiChatRole.Assistant ? ChatRole.Assistant : ChatRole.User, turn.Text));

        return new ChatRequest
        {
            Model = request.Model,
            Messages = messages,
            Stream = stream,
            Options = new RequestOptions { NumCtx = NumCtx },
        };
    }

    public async Task<string?> GenerateAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var chatRequest = BuildChatRequest(request, stream: false);

        try
        {
            // OllamaApiClient does not own/dispose an externally supplied HttpClient.
            var client = new OllamaApiClient(httpClientFactory.CreateClient(nameof(OllamaClient)));

            // ChatAsync yields response chunks; with Stream=false it's a single chunk. Accumulate
            // whatever content arrives so both shapes work.
            var sb = new StringBuilder();
            await foreach (var chunk in client.ChatAsync(chatRequest, cancellationToken))
                if (chunk?.Message?.Content is { Length: > 0 } content)
                    sb.Append(content);

            var text = sb.ToString().Trim();
            if (text.Length > 0)
                return text;

            logger.LogWarning("Ollama returned no text (model {Model})", request.Model);
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

    public async IAsyncEnumerable<string> GenerateStreamAsync(AiChatCompletionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chatRequest = BuildChatRequest(request, stream: true);
        var client = new OllamaApiClient(httpClientFactory.CreateClient(nameof(OllamaClient)));

        // Iterate the stream by hand so a mid-stream failure (server drop, timeout) can be swallowed
        // like GenerateAsync does — you can't `yield` inside a try/catch, so the catch wraps only the
        // MoveNext and the yield happens outside it.
        await using var enumerator = client.ChatAsync(chatRequest, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            string? delta = null;
            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;
                delta = enumerator.Current?.Message?.Content;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Ollama stream failed (model {Model}): {Error}", request.Model, ex.Message);
                yield break;
            }

            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }
}
