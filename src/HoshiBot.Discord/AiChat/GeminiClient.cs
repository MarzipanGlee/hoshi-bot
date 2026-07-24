using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Google.GenAI;
using Google.GenAI.Types;
using HoshiBot.Data;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// The Google Gemini backend (IAiChatProvider). Stateless from the caller's point of view: the API
// key and model come in per request (resolved per-guild by AiChatService from the DB), never from
// global config — every guild brings its own key.
//
// The SDK's Client owns an HttpClient, so we cache one Client per distinct API key (guilds are
// few and keys stable) rather than constructing one per message; the cache is static so it's
// shared across the scoped GeminiClient instances.
public class GeminiClient(ILogger<GeminiClient> logger) : IAiChatProvider
{
    public AiProvider Kind => AiProvider.Gemini;

    public string DefaultModel => AiBackendSettingKeys.DefaultModel;

    // Gemini gate is on by default: a flash-lite tier is cheap and always available to a valid key,
    // so the passive-listening cost/noise win lands with no per-guild setup. Overridable per guild.
    public string? DefaultGateModel => AiBackendSettingKeys.DefaultGateModel;

    // Cloud backend: latency is network-bound, not prompt-eval-bound, so use the full window.
    public int HistoryLimit => 15;
    public int KnowledgeSnippetLimit => 12;

    private static readonly ConcurrentDictionary<string, Client> ClientsByApiKey = new();

    // A hung/overloaded Gemini call otherwise runs to the SDK's 100s HttpClient default — far too long
    // for a chat reply (the asker just sees "typing…" for ~2 min, then a failure). Cap it so a slow or
    // "high demand" model fails fast and AiChatService's flash-lite failover can take over. Cancelling
    // our linked token trips the catch below (which returns null) rather than propagating.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public async Task<string?> GenerateAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
        => (await GenerateDetailedAsync(request, cancellationToken)).Text;

    // Returns the model's text answer (or null on any failure/empty response) plus a classification of
    // why it was null, so AiChatService can pick a friendlier reply for a transient overload/timeout.
    // Never throws for an API/network error.
    public async Task<AiChatGeneration> GenerateDetailedAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            logger.LogWarning("Gemini generation requested without an API key; staying silent.");
            return new AiChatGeneration(null, AiChatFailureKind.Other);
        }

        var client = ClientsByApiKey.GetOrAdd(request.ApiKey, key => new Client(apiKey: key));

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = request.SystemInstruction }] },
        };

        var contents = request.Turns
            .Select(t => new Content { Role = ToGeminiRole(t.Role), Parts = [new Part { Text = t.Text }] })
            .ToList();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            var response = await client.Models.GenerateContentAsync(request.Model, contents, config, timeoutCts.Token);
            var text = response.Text;
            if (!string.IsNullOrWhiteSpace(text))
                return new AiChatGeneration(text.Trim(), AiChatFailureKind.None);

            // A successful call that returns no text is usually a safety block or a truncated
            // ("thinking ate the budget") response — surface the reason so it's debuggable.
            var finishReason = response.Candidates?.FirstOrDefault()?.FinishReason;
            logger.LogWarning(
                "Gemini returned no text (model {Model}, finishReason {FinishReason}, promptFeedback {PromptFeedback})",
                request.Model, finishReason, response.PromptFeedback?.BlockReason);
            return new AiChatGeneration(null, AiChatFailureKind.Other);
        }
        // Swallow every provider-side failure (bad key, unknown model, quota, and a timeout —
        // which surfaces as a TaskCanceledException, a subclass of OperationCanceledException).
        // Only a genuine *caller* cancellation (our own token tripped) propagates. This is the
        // line to check when the bot only ever replies with the "can't answer" fallback.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Gemini request failed (model {Model}): {Error}", request.Model, ex.Message);
            return new AiChatGeneration(null, ClassifyFailure(ex));
        }
    }

    // A timeout (our linked-CTS cancel surfaces as OperationCanceledException) or an
    // overload/unavailable/quota signal in the error is transient — worth a friendly "busy, try again"
    // reply rather than a flat "can't answer". Everything else (bad key, unknown model, safety) is Other.
    private static AiChatFailureKind ClassifyFailure(Exception ex)
    {
        if (ex is OperationCanceledException)
            return AiChatFailureKind.Overloaded;

        string[] overloadSignals = ["high demand", "overloaded", "unavailable", "503", "resource_exhausted", "429", "try again"];
        return overloadSignals.Any(s => ex.Message.Contains(s, StringComparison.OrdinalIgnoreCase))
            ? AiChatFailureKind.Overloaded
            : AiChatFailureKind.Other;
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(AiChatCompletionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            logger.LogWarning("Gemini stream requested without an API key; staying silent.");
            yield break;
        }

        var client = ClientsByApiKey.GetOrAdd(request.ApiKey, key => new Client(apiKey: key));

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = request.SystemInstruction }] },
        };

        var contents = request.Turns
            .Select(t => new Content { Role = ToGeminiRole(t.Role), Parts = [new Part { Text = t.Text }] })
            .ToList();

        // Iterate by hand so a mid-stream failure is swallowed (never-throw contract) — `yield` can't
        // sit inside a try/catch, so the catch wraps only MoveNext.
        await using var enumerator = client.Models
            .GenerateContentStreamAsync(request.Model, contents, config, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            string? delta = null;
            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;
                delta = enumerator.Current?.Text;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Gemini stream failed (model {Model}): {Error}", request.Model, ex.Message);
                yield break;
            }

            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }

    // Gemini's own role names: a member is "user", a previous bot reply is "model".
    private static string ToGeminiRole(AiChatRole role) => role == AiChatRole.Assistant ? "model" : "user";
}
