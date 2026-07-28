using System.Text;
using HoshiBot.Data;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// One-off, admin-driven message composition in Hoshi's voice — the brain behind the "/hoshi-say"
// command. Unlike TryBuildReplyAsync (which decides *whether* to answer an incoming chat message and
// grounds the reply in retrieved knowledge), this takes an admin's explicit instruction ("what to
// convey") and just phrases it the way Hoshi would: same guild AI backend (provider/model/API key
// resolved from AiBackendSettingKeys, exactly like a chat reply) but a lean, persona-only system
// prompt with no retrieval. Returns null when the backend isn't usable (no Gemini key configured) or
// the model produced nothing, so the caller can surface a friendly "couldn't compose" instead of
// posting an empty message.
public partial class AiChatService
{
    // instruction: the admin's "what to convey". mentionUserId/mentionName (both optional, supplied
    // together): a member the message should be addressed to and *ping* — Hoshi is told to open with
    // the raw <@id> mention token, and the caller must allow that user in AllowedMentions for the ping
    // to fire. Without them she addresses people by name only and pings no one.
    public async Task<string?> ComposeMessageAsync(ulong guildId, string instruction, ulong? mentionUserId, string? mentionName, CancellationToken cancellationToken)
    {
        var provider = await ResolveProviderAsync(guildId);
        var apiKey = await settingsService.GetSecretAsync(guildId, BackendFeature, BackendScope, null, AiBackendSettingKeys.ApiKey);

        // Only Gemini authenticates per guild — the shared local Ollama needs no key.
        if (provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("ComposeMessageAsync for guild {GuildId} but no Gemini API key is configured.", guildId);
            return null;
        }

        var model = await settingsService.GetTextAsync(guildId, BackendFeature, BackendScope, null, AiBackendSettingKeys.Model);
        model = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();

        var botName = await embedBranding.GetBotDisplayNameAsync(guildId);

        var system = new StringBuilder();
        system.AppendLine(HoshiPersona.Describe(botName));
        system.AppendLine();
        // Give the composer the same current-date/environment awareness as a chat reply (no message
        // audience here, so the timezone falls back to the guild's primary alliance) — so an
        // admin-composed announcement uses the real date instead of inventing one.
        system.Append(await BuildEnvironmentContextAsync(guildId, allianceId: null, model, provider.Kind));
        system.AppendLine();
        system.AppendLine(
            "Ein Administrator bittet dich, eine Nachricht in einen Chat-Kanal der Community zu schreiben. " +
            "Der folgende Auftrag beschreibt, WAS du vermitteln sollst – formuliere daraus eine fertige " +
            "Nachricht in deiner eigenen Stimme, direkt an die Community bzw. das genannte Mitglied gerichtet " +
            "(nicht an den Administrator). Gib ausschließlich den fertigen Nachrichtentext zurück: ohne " +
            "Anführungszeichen, ohne Vorrede, ohne Erklärung, ohne Betreffzeile. Halte dich kurz und " +
            "natürlich, wie eine echte Chat-Nachricht (meist ein bis drei Sätze).");

        var mentionToken = mentionUserId is { } uid ? $"<@{uid}>" : null;
        if (mentionToken is not null)
        {
            system.AppendLine(
                $"Die Nachricht richtet sich an {mentionName}. Beginne die Nachricht mit der Erwähnung " +
                $"{mentionToken} (verwende genau diese Zeichenfolge unverändert – sie erwähnt und pingt die " +
                "Person) und sprich sie danach natürlich an. Verwende sonst keine weitere Ping-Syntax.");
        }
        else
        {
            system.AppendLine(
                "Verwende KEINE Ping-Syntax wie <@123> oder <@&123>; sprich Mitglieder höchstens beim Namen an.");
        }

        var turns = new[] { new AiChatTurn(AiChatRole.User, instruction) };
        var request = new AiChatCompletionRequest(model, system.ToString(), turns, apiKey);

        var text = await provider.GenerateAsync(request, cancellationToken);

        // Resilience: the main model can transiently fail (e.g. gemini-3.5-flash "experiencing high
        // demand"). Mirror the chat path's fallback and retry once on the lighter model before giving
        // up, so /hoshi-say isn't blocked by a momentary spike on the premium model.
        if (string.IsNullOrWhiteSpace(text)
            && provider.DefaultGateModel is { } fallbackModel
            && !string.Equals(model, fallbackModel, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("ComposeMessageAsync for guild {GuildId}: main model {Model} returned nothing; retrying on {Fallback}.", guildId, model, fallbackModel);
            text = await provider.GenerateAsync(request with { Model = fallbackModel }, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Trim();

        // Guarantee the ping even if the model dropped or garbled the exact mention token.
        if (mentionToken is not null && !text.Contains(mentionToken, StringComparison.Ordinal))
            text = $"{mentionToken} {text}";

        return text;
    }
}
