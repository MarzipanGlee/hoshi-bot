using System.Text.RegularExpressions;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Discord.AiChat;

// Backend/settings resolution and the cheap classifier calls: which provider/model answers, which
// audience's behavioral settings apply, the passive-listening YES/NO gate and the SIMPLE/COMPLEX
// router that decides whether the premium model is worth spending on a question.
public partial class AiChatService
{
    // Passive-listening gate: a one-word YES/NO classifier prompt (the message only, no knowledge
    // retrieval). Biased toward YES on doubt so only *obvious* non-questions are suppressed —
    // borderline cases fall through to the main model + [NO_ANSWER]. See the gate block in
    // TryBuildReplyAsync and ClassifyGate.
    private const string GateSystemPrompt =
        "Du bist ein Klassifikator für einen Discord-Assistenten einer Star-Trek-Fleet-Command-Allianz. " +
        "Entscheide, ob die folgende Nachricht eine an den Assistenten oder allgemein gerichtete, beantwortbare Sachfrage ist. " +
        "Antworte mit genau einem Wort: NO nur, wenn es eindeutig KEINE solche Frage ist (z. B. Begrüßung, Smalltalk, " +
        "Reaktion, Meinung, Aussage, Aufruf an die Allianz oder an andere Mitglieder). Sonst YES. Im Zweifel YES.";

    // Complexity router: classifies a to-be-answered message so a cheap model handles simple ones and
    // only complex ones escalate to the premium model. Biased toward SIMPLE on doubt (conserves the
    // scarce premium quota — e.g. Gemini flash's 20 requests/day). See the routing block in
    // TryBuildReplyAsync and ClassifyComplexity.
    private const string RouterSystemPrompt =
        "Du bist ein Klassifikator, der die Komplexität einer Frage an einen Discord-Assistenten " +
        "(Star-Trek-Fleet-Command-Allianz) einschätzt. Antworte mit genau einem Wort: COMPLEX oder SIMPLE. " +
        "COMPLEX = erfordert mehrschrittiges Schlussfolgern, Strategie/Planung, eine ausführliche Erklärung " +
        "oder ist breit bzw. mehrdeutig. SIMPLE = eine konkrete, eng umrissene Sach- oder Faktenfrage mit " +
        "kurzer Antwort. Im Zweifel SIMPLE.";

    // Audience precedence when a channel could match more than one, and for the addressed-in-an-
    // unconfigured-channel fallback.
    private static readonly GuildAudience[] AudiencePrecedence =
        [GuildAudience.Alliance, GuildAudience.Server, GuildAudience.VeilGroup, GuildAudience.Community];

    // The guild's configured chat backend: the explicit Provider setting parsed to AiProvider
    // (default Gemini on unset/unknown), matched against the registered providers.
    private async Task<IAiChatProvider> ResolveProviderAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, BackendFeature, BackendScope, null, AiBackendSettingKeys.Provider);
        var kind = Enum.TryParse<AiProvider>(configured, ignoreCase: true, out var parsed) ? parsed : AiProvider.Gemini;
        return providers.First(p => p.Kind == kind);
    }

    // Resolves which audience (and, for the Alliance audience, which alliance) the per-audience
    // behavioral settings should be read from for a message in this channel. AiChat's channels are
    // keyed per audience only, so the audience is the one whose listen/knowledge channels contain
    // the channel; the Alliance audience's specific alliance can't be derived from the channel, so
    // it falls back to the guild's primary linked alliance (exact for single-alliance guilds). A
    // directly-addressed message in a channel that isn't a configured source falls back to the first
    // enabled audience in precedence order.
    private async Task<SettingsScope> ResolveSettingsScopeAsync(ulong guildId, ulong channelId)
    {
        var enabled = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);

        GuildAudience? match = null;
        foreach (var audience in AudiencePrecedence)
        {
            if (!enabled.Contains(audience))
                continue;

            var inAudience =
                (await channelService.GetChannelsAsync(guildId, GuildFeature.AiChat, audience)).Contains(channelId)
                || (await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledge, audience)).Contains(channelId)
                || (await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledgePreferred, audience)).Contains(channelId)
                || (await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledgeLastResort, audience)).Contains(channelId);
            if (inAudience)
            {
                match = audience;
                break;
            }
        }

        // Fallback for an addressed message in a non-source channel: the first enabled audience.
        var resolved = match ?? AudiencePrecedence.FirstOrDefault(enabled.Contains);
        var allianceId = resolved == GuildAudience.Alliance ? await allianceService.GetPrimaryIdAsync(guildId) : null;
        return new SettingsScope(resolved, allianceId);
    }

    // The language a reply into this channel speaks — the channel's OWNING scope's language, not the
    // message author's (the whole channel reads a public answer): the alliance's language for an
    // Alliance-audience channel, the audience's for the other audiences, and the guild's when no
    // enabled audience matched (or the Alliance audience has no resolvable alliance).
    private async Task<Language> ResolveReplyLanguageAsync(ulong guildId, SettingsScope scope) => scope switch
    {
        { Audience: GuildAudience.Alliance, AllianceId: { } allianceId } => await languageResolver.ForAllianceAsync(allianceId),
        { Audience: GuildAudience.Server or GuildAudience.VeilGroup or GuildAudience.Community } => await languageResolver.ForAudienceAsync(guildId, scope.Audience),
        _ => await languageResolver.ForGuildAsync(guildId),
    };

    // Outcome of the passive-listening gate. Only No suppresses; the rest fall through to the main
    // model (Failed = the gate call errored/returned nothing, so we degrade to today's behaviour).
    private enum GateResult { Yes, No, Ambiguous, Failed }

    // The gate model for this guild: the explicit GateModel setting (the literal "off" disables the
    // gate), else the provider's default gate model (null when the provider has none — e.g. Ollama
    // with no Ollama:GateModel configured). Null ⇒ no gate, current behaviour.
    private async Task<string?> ResolveGateModelAsync(ulong guildId, IAiChatProvider provider)
    {
        var configured = await settingsService.GetTextAsync(guildId, BackendFeature, BackendScope, null, AiBackendSettingKeys.GateModel);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var trimmed = configured.Trim();
            return trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
        }

        return provider.DefaultGateModel;
    }

    // One cheap classification call — the message only, no knowledge retrieval and no typing
    // indicator. A null answer (provider error / model not pulled / empty) is treated as Failed and
    // falls through to the main model, so a missing/wrong gate model never breaks passive listening.
    private async Task<GateResult> EvaluateGateAsync(string gateModel, IAiChatProvider provider, string? apiKey, NetCord.User author, string content, CancellationToken cancellationToken)
    {
        var turn = new AiChatTurn(AiChatRole.User, $"{CommanderName.Of(author)}: {content}");
        var answer = await provider.GenerateAsync(new AiChatCompletionRequest(gateModel, GateSystemPrompt, [turn], apiKey), cancellationToken);
        if (answer is null)
        {
            logger.LogWarning("AiChat gate model {GateModel} (provider {Provider}) returned null; falling through to the main model.", gateModel, provider.Kind);
            return GateResult.Failed;
        }

        return ClassifyGate(answer);
    }

    // Lenient parse of the gate's one-word verdict: only a clear, unambiguous NO suppresses. A YES,
    // both words, or neither (garbage) errs toward answering — the strictly-additive bias.
    private static GateResult ClassifyGate(string answer)
    {
        var upper = answer.ToUpperInvariant();
        var no = GateNo().IsMatch(upper);
        var yes = GateYes().IsMatch(upper);
        if (no && !yes)
            return GateResult.No;
        if (yes && !no)
            return GateResult.Yes;
        return GateResult.Ambiguous;
    }

    // Opt-in per guild: answers stream (placeholder/typing → live edits) only when StreamResponses is
    // "true". Unset (default) → classic post-once.
    private async Task<bool> IsStreamingEnabledAsync(ulong guildId)
    {
        var value = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, _settingsScope.Audience, _settingsScope.AllianceId, AiChatSettingKeys.StreamResponses);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private enum Complexity { Simple, Complex }

    // The complexity-router model for this guild, or null when routing is off (RouterModel unset or
    // "off"). Opt-in: no provider fallback, so an existing guild's behaviour is unchanged until set.
    private async Task<string?> ResolveRouterModelAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, BackendFeature, BackendScope, null, AiBackendSettingKeys.RouterModel);
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        var trimmed = configured.Trim();
        return trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    // One cheap classification call (message only) → SIMPLE or COMPLEX. Errs to SIMPLE on failure so a
    // broken/missing router model never escalates to (and drains) the scarce premium-model quota.
    private async Task<Complexity> EvaluateComplexityAsync(string routerModel, IAiChatProvider provider, string? apiKey, NetCord.User author, string content, CancellationToken cancellationToken)
    {
        var turn = new AiChatTurn(AiChatRole.User, $"{CommanderName.Of(author)}: {content}");
        var answer = await provider.GenerateAsync(new AiChatCompletionRequest(routerModel, RouterSystemPrompt, [turn], apiKey), cancellationToken);
        if (answer is null)
        {
            logger.LogWarning("AiChat router model {RouterModel} (provider {Provider}) returned null; treating as SIMPLE.", routerModel, provider.Kind);
            return Complexity.Simple;
        }

        return ClassifyComplexity(answer);
    }

    // Only a clear, unambiguous COMPLEX escalates to the premium model; SIMPLE, both words, or neither
    // (garbage) stays on the cheap router model — the quota-conserving bias.
    private static Complexity ClassifyComplexity(string answer)
    {
        var upper = answer.ToUpperInvariant();
        return ComplexWord().IsMatch(upper) && !SimpleWord().IsMatch(upper) ? Complexity.Complex : Complexity.Simple;
    }

    // Per-guild FTS config: the explicit setting, else derived from the guild's Discord locale,
    // else "simple". Always normalized against the supported whitelist before use.
    private async Task<string> ResolveSearchLanguageAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, _settingsScope.Audience, _settingsScope.AllianceId, AiChatSettingKeys.SearchLanguage);
        if (!string.IsNullOrWhiteSpace(configured))
            return FtsLanguage.Normalize(configured);

        var locale = gatewayClient.Cache.Guilds.GetValueOrDefault(guildId)?.PreferredLocale;
        return FtsLanguage.FromDiscordLocale(locale);
    }

    // Gate-verdict tokens, matched as whole words on the upper-cased answer (JA/NEIN included in
    // case a model answers in German despite the one-word YES/NO instruction).
    [GeneratedRegex(@"\b(NO|NEIN)\b")]
    private static partial Regex GateNo();

    [GeneratedRegex(@"\b(YES|JA)\b")]
    private static partial Regex GateYes();

    // Complexity-router verdict tokens, matched as whole words on the upper-cased answer.
    [GeneratedRegex(@"\bCOMPLEX\b")]
    private static partial Regex ComplexWord();

    [GeneratedRegex(@"\bSIMPLE\b")]
    private static partial Regex SimpleWord();
}
