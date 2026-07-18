using System.Collections.Concurrent;
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

    public string DefaultModel => AiChatSettingKeys.DefaultModel;

    // Gemini gate is on by default: a flash-lite tier is cheap and always available to a valid key,
    // so the passive-listening cost/noise win lands with no per-guild setup. Overridable per guild.
    public string? DefaultGateModel => AiChatSettingKeys.DefaultGateModel;

    // Cloud backend: latency is network-bound, not prompt-eval-bound, so use the full window.
    public int HistoryLimit => 15;
    public int KnowledgeSnippetLimit => 12;

    private static readonly ConcurrentDictionary<string, Client> ClientsByApiKey = new();

    // Returns the model's text answer, or null on any failure/empty response (the caller decides
    // what a null means — usually "stay silent"). Never throws for an API/network error.
    public async Task<string?> GenerateAsync(AiChatCompletionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            logger.LogWarning("Gemini generation requested without an API key; staying silent.");
            return null;
        }

        var client = ClientsByApiKey.GetOrAdd(request.ApiKey, key => new Client(apiKey: key));

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = request.SystemInstruction }] },
        };

        var contents = request.Turns
            .Select(t => new Content { Role = ToGeminiRole(t.Role), Parts = [new Part { Text = t.Text }] })
            .ToList();

        try
        {
            var response = await client.Models.GenerateContentAsync(request.Model, contents, config, cancellationToken);
            var text = response.Text;
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();

            // A successful call that returns no text is usually a safety block or a truncated
            // ("thinking ate the budget") response — surface the reason so it's debuggable.
            var finishReason = response.Candidates?.FirstOrDefault()?.FinishReason;
            logger.LogWarning(
                "Gemini returned no text (model {Model}, finishReason {FinishReason}, promptFeedback {PromptFeedback})",
                request.Model, finishReason, response.PromptFeedback?.BlockReason);
            return null;
        }
        // Swallow every provider-side failure (bad key, unknown model, quota, and a timeout —
        // which surfaces as a TaskCanceledException, a subclass of OperationCanceledException).
        // Only a genuine *caller* cancellation (our own token tripped) propagates. This is the
        // line to check when the bot only ever replies with the "can't answer" fallback.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Gemini request failed (model {Model}): {Error}", request.Model, ex.Message);
            return null;
        }
    }

    // Gemini's own role names: a member is "user", a previous bot reply is "model".
    private static string ToGeminiRole(AiChatRole role) => role == AiChatRole.Assistant ? "model" : "user";
}
