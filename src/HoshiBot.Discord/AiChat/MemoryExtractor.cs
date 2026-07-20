using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// Distils a slice of recent community chat into a few durable "episodic" memories — notable events
// worth remembering (a war, a win/loss, someone joining/leaving, an event, a memorable moment), each
// with a 1–5 salience. Deliberately conservative: most chatter yields nothing. Runs one lightweight
// LLM completion with a JSON-only prompt and parses it. Pure text-in/data-out; the job embeds/stores.
public class MemoryExtractor(ILogger<MemoryExtractor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public sealed record MemoryItem(string? Content, int Salience);

    private sealed record ExtractionResult(List<MemoryItem>? Memories);

    public async Task<List<MemoryItem>> ExtractAsync(ResolvedAiChatModel model, string conversationText, CancellationToken cancellationToken)
    {
        var systemPrompt =
            "Du bist das Gedächtnis einer Star-Trek-Fleet-Command-Allianz-Community. Lies den folgenden Chat-Ausschnitt " +
            "und extrahiere NUR wirklich bemerkenswerte, dauerhaft erinnernswerte Ereignisse oder Fakten über die Allianz " +
            "bzw. die Community: z. B. Kriege, Siege/Niederlagen, Gebietsübernahmen, Ein- oder Austritte von Mitgliedern, " +
            "Events, Ankündigungen, denkwürdige/lustige Momente. Ignoriere Smalltalk, Tagesgeschäft, Begrüßungen und alles " +
            "Belanglose. Erfinde nichts, fasse dich knapp (ein Satz pro Erinnerung), keine sensiblen privaten Daten.\n\n" +
            "Gib AUSSCHLIESSLICH gültiges JSON in genau dieser Form zurück (kein Markdown, keine Code-Fences):\n" +
            "{ \"memories\": [ { \"content\": string, \"salience\": 1-5 } ] }\n" +
            "salience: 5 = sehr bedeutend/langlebig, 1 = nur am Rande erwähnenswert. Wenn nichts Bemerkenswertes vorkommt, " +
            "gib eine leere Liste zurück.";

        var userTurn = new AiChatTurn(AiChatRole.User, conversationText);
        var raw = await model.Provider.GenerateAsync(
            new AiChatCompletionRequest(model.Model, systemPrompt, [userTurn], model.ApiKey), cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var json = ExtractJsonObject(raw);
        if (json is null)
        {
            logger.LogWarning("Memory extraction produced no parseable JSON: {Raw}", Truncate(raw, 300));
            return [];
        }

        try
        {
            var result = JsonSerializer.Deserialize<ExtractionResult>(json, JsonOptions);
            return (result?.Memories ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => m with { Content = m.Content!.Trim(), Salience = Math.Clamp(m.Salience is 0 ? 3 : m.Salience, 1, 5) })
                .ToList();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Memory extraction JSON failed to deserialize: {Json}", Truncate(json, 300));
            return [];
        }
    }

    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
