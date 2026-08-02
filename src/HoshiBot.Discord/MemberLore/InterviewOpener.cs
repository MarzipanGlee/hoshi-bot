using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Localization;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.MemberLore;

// The member-lore interview's first DM. Unlike the rest of the fixed interview messages (decline
// button, closers) this one is NOT a catalog entry: it names the alliance and the bot's own display
// name, and it's a single paragraph we expect to keep tuning, so maintaining an en/de pair by hand
// buys nothing. Instead it's one English constant translated on the fly — the same thin,
// single-purpose wrapper shape as AnnouncementTranslator.
public class InterviewOpener(ILogger<InterviewOpener> logger)
{
    // Deliberately short: introduce Hoshi as the alliance's communication officer, ask for a moment,
    // say any language is fine, and keep the opt-out tail pointing at the decline button. It asks no
    // interview questions at all — BuildInterviewPrompt asks those properly, one turn at a time.
    private const string Template =
        "🖖 Hi! I'm {botName}, communication officer of {alliance}. Do you have a short moment to " +
        "chat? I'd love to get to know you a little better — and don't worry about the language, " +
        "just write in whatever you're comfortable with, I speak them all. 😄\n\n" +
        "Completely optional — if you're not in the mood right now, just click the No-thanks button " +
        "below and I'll leave you alone.";

    // Renders the opener in the member's language. Never returns null: if the model is unavailable or
    // answers with nothing, the English original goes out rather than stalling the campaign — the
    // interview itself mirrors whatever language the member replies in anyway.
    public async Task<string> RenderAsync(
        ResolvedAiChatModel model,
        string botName,
        string allianceName,
        Language lang,
        CancellationToken cancellationToken)
    {
        var opener = Template
            .Replace("{botName}", botName, StringComparison.Ordinal)
            .Replace("{alliance}", allianceName, StringComparison.Ordinal);

        if (lang == Language.En)
            return opener;

        // Names are substituted before translating, so the model only ever sees finished prose — a
        // placeholder left in the text is one more thing a translation can mangle.
        var targetLanguage = Languages.EnglishName(lang);
        var systemPrompt =
            $"Translate the following short, friendly Discord direct message into {targetLanguage}. " +
            "Keep the warm, casual tone and the meaning exactly. Return only the translation — no " +
            "explanations, no quotation marks, no extra Markdown. The emojis, the blank line between " +
            $"the two paragraphs, and the two proper nouns (\"{botName}\" and \"{allianceName}\") must " +
            "stay unchanged and in the same place. \"No-thanks button\" refers to a Discord button " +
            "the member can click, so translate it the way that button's label would read in " +
            $"{targetLanguage}. The speaker is female: wherever {targetLanguage} marks gender, use " +
            "the feminine form for her (German \"Kommunikationsoffizierin\", not " +
            "\"Kommunikationsoffizier\"). The English source doesn't mark it, so without this the " +
            "form comes out differently on every call.";

        var translated = (await model.Provider.GenerateAsync(
            new AiChatCompletionRequest(model.Model, systemPrompt, [new AiChatTurn(AiChatRole.User, opener)], model.ApiKey),
            cancellationToken))?.Trim();

        if (!string.IsNullOrWhiteSpace(translated))
            return translated;

        logger.LogWarning("Interview opener could not be translated to {Language}; sending the English original.", targetLanguage);
        return opener;
    }
}
