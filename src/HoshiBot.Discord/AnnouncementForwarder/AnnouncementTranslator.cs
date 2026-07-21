using HoshiBot.Discord.AiChat;

namespace HoshiBot.Discord.AnnouncementForwarder;

// Translates one announcement's text into the guild's target language for the forwarder. A thin,
// single-purpose wrapper (like MemberNoteExtractor) rather than a general-purpose translation
// service — the prompt is tuned specifically for "official game announcement" text.
public class AnnouncementTranslator
{
    // Returned verbatim by the model when the source text is already in the target language, so the
    // caller can skip posting a redundant "translation".
    private const string AlreadyTargetSentinel = "[ALREADY_TARGET]";

    public async Task<string?> TranslateAsync(ResolvedAiChatModel model, string sourceText, string targetLanguage, CancellationToken cancellationToken)
    {
        var systemPrompt =
            $"Du übersetzt eine offizielle Spiel-Ankündigung für eine Star-Trek-Fleet-Command-Allianz-Community. " +
            $"Übersetze den folgenden Text vollständig und natürlich nach {targetLanguage}. Erhalte die Bedeutung " +
            "exakt, aber gib nur die Übersetzung zurück — keine Erklärungen, keine Anführungszeichen, kein Markdown " +
            "außer dem, was bereits im Original steht. Discord-Auszeichnungen wie **fett**, Zeilenumbrüche, Emojis " +
            "und Platzhalter der Form <t:1234567890:t> oder <t:1234567890:F> müssen UNVERÄNDERT und an derselben " +
            $"Stelle erhalten bleiben (nie übersetzen oder verändern). Wenn der Text bereits komplett auf " +
            $"{targetLanguage} verfasst ist, antworte ausschließlich mit exakt {AlreadyTargetSentinel} und sonst nichts.";

        var userTurn = new AiChatTurn(AiChatRole.User, sourceText);
        var translated = (await model.Provider.GenerateAsync(
            new AiChatCompletionRequest(model.Model, systemPrompt, [userTurn], model.ApiKey), cancellationToken))?.Trim();

        if (string.IsNullOrWhiteSpace(translated) || translated.Contains(AlreadyTargetSentinel, StringComparison.OrdinalIgnoreCase))
            return null;
        return translated;
    }
}
