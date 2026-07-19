using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HoshiBot.Discord.AiChat;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.MemberLore;

// Turns a finished interview transcript into structured note material: the interviewee's own bio
// fields plus any lore they volunteered about *other* members. Runs one LLM completion with a
// JSON-only extraction prompt (reusing the guild's AI-chat model) and parses the result. Pure
// text-in/data-out — the job owns the DB writes and target resolution. See docs/ai-chat-member-lore.md.
public class MemberNoteExtractor(ILogger<MemberNoteExtractor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public sealed record ExtractionResult(SelfInfo? Self, List<PeerInfo> Peers);

    public sealed record SelfInfo(
        string? PreferredName,
        List<string>? Nicknames,
        string? Interests,
        string? Background,
        string? Languages);

    // A story/fact the interviewee told about a different member. Field is one of runningJokes /
    // teaseAbout / interests (defaulted to runningJokes if the model returns something else).
    public sealed record PeerInfo(string? Name, string? Field, string? Text);

    public async Task<ExtractionResult?> ExtractAsync(
        ResolvedAiChatModel model,
        string intervieweeName,
        IReadOnlyList<AiChatTurn> transcript,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt(intervieweeName);
        var userTurn = new AiChatTurn(AiChatRole.User, FormatTranscript(intervieweeName, transcript));

        var raw = await model.Provider.GenerateAsync(
            new AiChatCompletionRequest(model.Model, systemPrompt, [userTurn], model.ApiKey), cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            logger.LogWarning("Member-note extraction returned nothing for interviewee {Name}.", intervieweeName);
            return null;
        }

        var json = ExtractJsonObject(raw);
        if (json is null)
        {
            logger.LogWarning("Member-note extraction produced no parseable JSON for {Name}: {Raw}", intervieweeName, Truncate(raw, 400));
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<ExtractionResult>(json, JsonOptions);
            return result is null ? null : result with { Peers = result.Peers ?? [] };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Member-note extraction JSON failed to deserialize for {Name}: {Json}", intervieweeName, Truncate(json, 400));
            return null;
        }
    }

    private static string BuildSystemPrompt(string intervieweeName) =>
        $"Du wertest ein freundliches Kennenlern-Gespräch aus, das der Bot mit dem Mitglied „{intervieweeName}“ geführt hat. " +
        "Extrahiere daraus kompakte Community-Notizen. Gib AUSSCHLIESSLICH gültiges JSON in genau dieser Form zurück " +
        "(keine Erklärungen, kein Markdown, keine Code-Fences):\n" +
        "{\n" +
        "  \"self\": { \"preferredName\": string|null, \"nicknames\": string[]|null, \"interests\": string|null, \"background\": string|null, \"languages\": string|null },\n" +
        "  \"peers\": [ { \"name\": string, \"field\": \"runningJokes\"|\"teaseAbout\"|\"interests\", \"text\": string } ]\n" +
        "}\n\n" +
        $"„self“ beschreibt {intervieweeName} selbst: bevorzugter Name (wie angesprochen werden), Spitznamen, Interessen/Hobbys " +
        "(im Spiel und privat), kurzer Hintergrund (Beruf o. Ä., nur was geteilt wurde) und Sprachen. Lass ein Feld null, wenn nichts dazu gesagt wurde.\n" +
        "„peers“ sind Geschichten oder Fakten, die das Mitglied über ANDERE Spieler erzählt hat — je Eintrag der genannte Name, " +
        "ob es ein Running Gag / neckisch (\"runningJokes\"), ein „darf man necken wegen …“ (\"teaseAbout\") oder ein Interesse (\"interests\") ist, und der kurze Text.\n" +
        "Halte alles knapp, wohlwollend und faktisch. Erfinde nichts. Keine harten psychologischen oder Leistungs-Urteile. " +
        "Wenn keine Peer-Infos vorkommen, gib eine leere Liste zurück.";

    private static string FormatTranscript(string intervieweeName, IReadOnlyList<AiChatTurn> transcript)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Gesprächsverlauf:");
        foreach (var turn in transcript)
        {
            var speaker = turn.Role == AiChatRole.Assistant ? "Hoshi" : intervieweeName;
            sb.AppendLine($"{speaker}: {turn.Text}");
        }
        return sb.ToString();
    }

    // Models occasionally wrap the JSON in prose or ```json fences — grab the outermost {...} span.
    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
