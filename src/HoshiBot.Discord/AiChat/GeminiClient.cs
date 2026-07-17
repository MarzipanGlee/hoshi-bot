using System.Collections.Concurrent;
using Google.GenAI;
using Google.GenAI.Types;
using HoshiBot.Data;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// Thin wrapper over Google's official GenAI SDK (Google.GenAI). Stateless from the caller's
// point of view: the API key and model are passed in per call (resolved per-guild by
// AiChatService from the DB), never read from global config — every guild brings its own key.
//
// The SDK's Client owns an HttpClient, so we cache one Client per distinct API key (guilds are
// few and keys stable) rather than constructing one per message; the cache is static so it's
// shared across the scoped GeminiClient instances.
public class GeminiClient(ILogger<GeminiClient> logger)
{
    public const string DefaultModel = AiChatSettingKeys.DefaultModel;

    private static readonly ConcurrentDictionary<string, Client> ClientsByApiKey = new();

    // One conversation turn. Role is "user" (a member) or "model" (a previous bot reply) —
    // Gemini's own role names.
    public readonly record struct Turn(string Role, string Text);

    // Returns the model's text answer, or null on any failure/empty response (the caller decides
    // what a null means — usually "stay silent"). Never throws for an API/network error.
    public async Task<string?> GenerateAsync(
        string apiKey,
        string model,
        string systemInstruction,
        IReadOnlyList<Turn> turns,
        CancellationToken cancellationToken)
    {
        var client = ClientsByApiKey.GetOrAdd(apiKey, key => new Client(apiKey: key));

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = systemInstruction }] },
        };

        var contents = turns
            .Select(t => new Content { Role = t.Role, Parts = [new Part { Text = t.Text }] })
            .ToList();

        try
        {
            var response = await client.Models.GenerateContentAsync(model, contents, config, cancellationToken);
            var text = response.Text;
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();

            // A successful call that returns no text is usually a safety block or a truncated
            // ("thinking ate the budget") response — surface the reason so it's debuggable.
            var finishReason = response.Candidates?.FirstOrDefault()?.FinishReason;
            logger.LogWarning(
                "Gemini returned no text (model {Model}, finishReason {FinishReason}, promptFeedback {PromptFeedback})",
                model, finishReason, response.PromptFeedback?.BlockReason);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log the actual message (bad key, unknown model, quota, etc.) — this is the line to
            // check when the bot only ever replies with the "can't answer" fallback.
            logger.LogWarning(ex, "Gemini request failed (model {Model}): {Error}", model, ex.Message);
            return null;
        }
    }
}
