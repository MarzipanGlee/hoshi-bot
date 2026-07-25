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
    public async Task<string?> ComposeMessageAsync(ulong guildId, string instruction, CancellationToken cancellationToken)
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
        system.AppendLine(
            "Ein Administrator bittet dich, eine Nachricht in einen Chat-Kanal der Community zu schreiben. " +
            "Der folgende Auftrag beschreibt, WAS du vermitteln sollst – formuliere daraus eine fertige " +
            "Nachricht in deiner eigenen Stimme, direkt an die Community bzw. das genannte Mitglied gerichtet " +
            "(nicht an den Administrator). Gib ausschließlich den fertigen Nachrichtentext zurück: ohne " +
            "Anführungszeichen, ohne Vorrede, ohne Erklärung, ohne Betreffzeile. Halte dich kurz und " +
            "natürlich, wie eine echte Chat-Nachricht (meist ein bis drei Sätze). Verwende KEINE Ping-Syntax " +
            "wie <@123> oder <@&123>; sprich Mitglieder höchstens beim Namen an.");

        var turns = new[] { new AiChatTurn(AiChatRole.User, instruction) };
        var request = new AiChatCompletionRequest(model, system.ToString(), turns, apiKey);

        var text = await provider.GenerateAsync(request, cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
