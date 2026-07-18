using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.AiChat;

// The per-guild AI chat brain. Given an incoming guild message it decides whether to answer and,
// if so, builds the reply text — gathering recent channel history (the short conversational
// memory) plus the relevant knowledge (via the full-text index, AiChatIndexService), then calling
// Gemini. Returns null whenever the bot should stay silent, so the gateway handler stays a thin
// "reply if non-null".
//
// Gating: the AiChat feature must be enabled for the guild, and the message must be in a
// configured listen channel OR directly address the bot (by @mention or by its nickname / a part
// of it). A direct address always gets an answer; passive listening only answers when Gemini
// produces a genuinely helpful, grounded reply (otherwise it emits the NoAnswerSentinel and we
// stay silent).
public partial class AiChatService(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureChannelService channelService,
    GuildFeatureSettingsService settingsService,
    EmbedBranding embedBranding,
    IEnumerable<IAiChatProvider> providers,
    AiChatIndexService indexService,
    ILogger<AiChatService> logger)
{
    private const string NoAnswerSentinel = "[NO_ANSWER]";
    private const int DiscordMessageLimit = 2000;

    // Passive-listening gate: a one-word YES/NO classifier prompt (the message only, no knowledge
    // retrieval). Biased toward YES on doubt so only *obvious* non-questions are suppressed —
    // borderline cases fall through to the main model + [NO_ANSWER]. See the gate block in
    // TryBuildReplyAsync and ClassifyGate.
    private const string GateSystemPrompt =
        "Du bist ein Klassifikator für einen Discord-Assistenten einer Star-Trek-Fleet-Command-Allianz. " +
        "Entscheide, ob die folgende Nachricht eine an den Assistenten oder allgemein gerichtete, beantwortbare Sachfrage ist. " +
        "Antworte mit genau einem Wort: NO nur, wenn es eindeutig KEINE solche Frage ist (z. B. Begrüßung, Smalltalk, " +
        "Reaktion, Meinung, Aussage, Aufruf an die Allianz oder an andere Mitglieder). Sonst YES. Im Zweifel YES.";

    // Discord's typing indicator lasts ~10s; re-trigger a bit before that so it stays visible
    // across a slow (CPU-only Ollama) generation instead of stopping mid-wait.
    private static readonly TimeSpan TypingRefreshInterval = TimeSpan.FromSeconds(8);

    // The three scalar settings (API key, system prompt, model) plus the search language are
    // guild-wide — one Gemini account per guild — so they live at the None/null scope regardless of
    // which audiences the feature is enabled for (same pattern as ClientRelease's guild-wide
    // platform roles). The channel lists, by contrast, are per-audience.
    private const GuildAudience SettingsScope = GuildAudience.None;

    // One in-flight answer per channel — a passive listener could otherwise fire several
    // overlapping (and billable) Gemini calls for a burst of messages in the same channel.
    private static readonly ConcurrentDictionary<ulong, byte> InFlightChannels = new();

    // The reply text plus the exact set of user ids the reply is allowed to actually ping — the
    // conversation participants we told the model about. Discord's allowed_mentions is set to just
    // these, so a hallucinated or unknown <@id> in the text can never ping a random member.
    public readonly record struct AiChatReply(string Text, IReadOnlyList<ulong> AllowedUserIds);

    // Returns the reply to post, or null to stay silent.
    public async Task<AiChatReply?> TryBuildReplyAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.GuildId is not { } guildId)
            return null;
        if (message.Author.IsBot)
            return null;
        if (message.Type is not (MessageType.Default or MessageType.Reply))
            return null;

        var content = message.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            return null;

        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.AiChat))
            return null;

        var botId = gatewayClient.Id;
        var botName = await embedBranding.GetBotDisplayNameAsync(guildId);
        var addressed = message.MentionedUsers.Any(u => u.Id == botId) || MentionsBotByName(content, botName);

        var listenChannels = await channelService.GetEnabledAudienceChannelsAsync(guildId, GuildFeature.AiChat);
        var inListenChannel = listenChannels.Contains(message.ChannelId);
        if (!inListenChannel && !addressed)
            return null;

        // Passive listening only. A message that pings other members, a role, or @everyone/@here
        // is a member-to-member call (e.g. rallying the alliance to a task) — not a question for
        // the bot — so stay out of it. A direct address (bot @mention or nickname) still always
        // answers, even if it also mentions others.
        if (!addressed && (message.MentionEveryone
            || message.MentionedRoleIds.Count > 0
            || message.MentionedUsers.Any(u => u.Id != botId)))
        {
            return null;
        }

        var provider = await ResolveProviderAsync(guildId);
        var apiKey = await settingsService.GetSecretAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.ApiKey);

        // Only Gemini authenticates per guild — the shared local Ollama needs no key.
        if (provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("AiChat enabled for guild {GuildId} but no Gemini API key is configured; staying silent.", guildId);
            return null;
        }

        if (!InFlightChannels.TryAdd(message.ChannelId, 0))
            return null;

        try
        {
            // Passive-listening gate: a cheap small-model YES/NO classifier that runs BEFORE the
            // expensive knowledge retrieval + main generation (and before the typing indicator, so a
            // non-answer never shows phantom "typing…"). It only ever *suppresses* on a confident NO
            // — a YES, an ambiguous verdict, or a gate failure all fall through to the main model
            // (which can still emit [NO_ANSWER]), so the gate is strictly additive: quieter on
            // obvious chatter, never less capable. A direct address skips it (it always answers).
            var gateLabel = "skip";
            if (!addressed)
            {
                var gateModel = await ResolveGateModelAsync(guildId, provider);
                if (gateModel is null)
                {
                    gateLabel = "off";
                }
                else
                {
                    var gate = await EvaluateGateAsync(gateModel, provider, apiKey, message.Author, content, cancellationToken);
                    gateLabel = gate.ToString().ToLowerInvariant();
                    if (gate == GateResult.No)
                    {
                        logger.LogInformation(
                            "AiChat guild {Guild} ch {Channel}: passive gate={GateModel} → no → silent", guildId, message.ChannelId, gateModel);
                        return null;
                    }
                }
            }

            var model = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.Model);
            model = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();

            var systemExtra = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.SystemPrompt);

            var history = await indexService.FetchRecentAsync(message.ChannelId, provider.HistoryLimit, cancellationToken);
            history.Reverse(); // chronological
            var botSpokeBefore = history.Any(m => m.Author.Id == botId && m.Id != message.Id);

            // The users the bot may ping: the conversation's participants. The model is given
            // "name: <@id>" for these and told to only ping from this list; the handler restricts
            // Discord's allowed_mentions to exactly these ids.
            var mentionable = new Dictionary<ulong, string>();
            foreach (var m in history)
                if (m.Author.Id != botId)
                    mentionable[m.Author.Id] = CommanderName.Of(m.Author);
            mentionable[message.Author.Id] = CommanderName.Of(message.Author);

            // Prior context from the recent window (the short conversational memory), excluding the
            // triggering message — we append that ourselves below so the actual question is always
            // the final user turn even if the REST fetch hasn't caught up to it yet.
            var turns = new List<AiChatTurn>();
            foreach (var m in history)
            {
                if (m.Id == message.Id)
                    continue;
                var text = AiChatIndexService.RenderMessageText(m);
                if (string.IsNullOrEmpty(text))
                    continue;
                turns.Add(m.Author.Id == botId
                    ? new AiChatTurn(AiChatRole.Assistant, text)
                    : new AiChatTurn(AiChatRole.User, $"{CommanderName.Of(m.Author)}: {text}"));
            }

            turns.Add(new AiChatTurn(AiChatRole.User, $"{CommanderName.Of(message.Author)}: {content}"));

            var systemInstruction = await BuildSystemInstructionAsync(guildId, botName, systemExtra, addressed, content, mentionable, provider.KnowledgeSnippetLimit, cancellationToken);

            // Keep the typing indicator alive for the whole (potentially minute-long, CPU-only)
            // generation, then stop it as soon as we have an answer.
            string? answer;
            using (var typingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var typing = KeepTypingAsync(message.ChannelId, typingCts.Token);
                try
                {
                    answer = await provider.GenerateAsync(
                        new AiChatCompletionRequest(model, systemInstruction, turns, apiKey), cancellationToken);
                }
                finally
                {
                    await typingCts.CancelAsync();
                    try { await typing; } catch (OperationCanceledException) { /* expected on stop */ }
                }
            }

            var replyText = FinalizeAnswer(answer, addressed, botSpokeBefore, message.Author, botName);

            // One line per handled message so a "why did it stay silent / only give the fallback"
            // question is answerable straight from the logs.
            logger.LogInformation(
                "AiChat guild {Guild} ch {Channel}: addressed={Addressed} inListen={InListen} gate={Gate} turns={Turns} provider={Provider} model={Model} → answer={AnswerChars} reply={Reply}",
                guildId, message.ChannelId, addressed, inListenChannel, gateLabel, turns.Count, provider.Kind, model,
                answer?.Length.ToString() ?? "null", replyText is null ? "silent" : "posted");

            return replyText is null ? null : new AiChatReply(replyText, mentionable.Keys.ToList());
        }
        finally
        {
            InFlightChannels.TryRemove(message.ChannelId, out _);
        }
    }

    private string? FinalizeAnswer(string? answer, bool addressed, bool botSpokeBefore, NetCord.User author, string botName)
    {
        if (answer is null)
            return addressed ? PolitelyUnsure(botSpokeBefore, author) : null;

        var punted = answer.Contains(NoAnswerSentinel, StringComparison.OrdinalIgnoreCase);
        answer = answer.Replace(NoAnswerSentinel, "", StringComparison.OrdinalIgnoreCase).Trim();

        // Small models sometimes echo the "Name: text" roster format and open with their own
        // name (e.g. "Hoshi Sato: ..."). Strip that self-prefix before anything else.
        answer = StripSelfNamePrefix(answer, botName);

        if (punted || answer.Length == 0)
            return addressed ? PolitelyUnsure(botSpokeBefore, author) : null;

        // The first bot reply in a conversation opens with the "Commander {name}," convention.
        if (!botSpokeBefore && !answer.StartsWith("Commander", StringComparison.OrdinalIgnoreCase))
            answer = CommanderName.Greeting(author) + answer;

        return Truncate(answer);
    }

    // Removes a leading "<bot name>:" (the full display name or its first token, optional space
    // before the colon), case-insensitive — a habit small models pick up from the roster format.
    private static string StripSelfNamePrefix(string answer, string botName)
    {
        var candidates = new List<string> { botName };
        var firstToken = botName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrEmpty(firstToken) && !string.Equals(firstToken, botName, StringComparison.Ordinal))
            candidates.Add(firstToken);

        foreach (var name in candidates)
        {
            if (!answer.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                continue;
            var rest = answer[name.Length..].TrimStart();
            if (rest.StartsWith(':'))
                return rest[1..].TrimStart();
        }

        return answer;
    }

    // When the bot is addressed directly it must always say something, even if it has no real
    // answer — greet on the first turn just like a real reply.
    private static string PolitelyUnsure(bool botSpokeBefore, NetCord.User author)
    {
        const string body = "das kann ich dir leider nicht beantworten.";
        return botSpokeBefore ? char.ToUpper(body[0]) + body[1..] : CommanderName.Greeting(author) + body;
    }

    // Re-triggers the typing indicator every ~8s (it expires after ~10s) until cancelled, so it
    // stays visible across a slow generation. Cancelled by the caller as soon as the answer is in.
    private async Task KeepTypingAsync(ulong channelId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try { await gatewayClient.Rest.TriggerTypingAsync(channelId, cancellationToken: cancellationToken); }
                catch (RestException) { /* non-fatal */ }
                await Task.Delay(TypingRefreshInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) { /* stopped once the answer is ready */ }
    }

    private async Task<string> BuildSystemInstructionAsync(ulong guildId, string botName, string? systemExtra, bool addressed, string questionText, IReadOnlyDictionary<ulong, string> mentionable, int knowledgeSnippetLimit, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Du bist {botName}, ein hilfreicher Assistent für diese Discord-Community (ein Star-Trek-Fleet-Command-Allianz-Server).");
        sb.AppendLine("Antworte auf Deutsch, freundlich und knapp. Nutze zum Beantworten in erster Linie die unten angegebenen Wissensquellen und den bisherigen Chatverlauf.");

        if (!string.IsNullOrWhiteSpace(systemExtra))
        {
            sb.AppendLine();
            sb.AppendLine(systemExtra.Trim());
        }

        if (mentionable.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Bekannte Nutzer. Um jemanden zu erwähnen oder anzupingen, verwende exakt die Syntax <@ID> mit einer ID aus dieser Liste (niemals eine ID erfinden, niemals @Name als reinen Text schreiben):");
            foreach (var (id, name) in mentionable)
                sb.AppendLine($"- {name}: <@{id}>");
        }

        var knowledge = await BuildKnowledgeBlockAsync(guildId, questionText, knowledgeSnippetLimit, cancellationToken);
        if (knowledge.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Wissensquellen (relevante Auszüge; der Herkunftskanal steht als Link <#ID> in eckigen Klammern voran):");
            sb.Append(knowledge);
            sb.AppendLine();
            sb.AppendLine("Wenn du auf einen Kanal verweist, verwende exakt die Discord-Link-Syntax <#ID> mit einer ID aus den Wissensquellen (Discord macht daraus einen klickbaren Link). Schreibe niemals [#Name] oder #Name als reinen Text und erfinde keine IDs.");
        }

        sb.AppendLine();
        sb.AppendLine(addressed
            ? "Du wirst in dieser Nachricht direkt angesprochen. Antworte immer. Wenn du etwas nicht sicher weißt, sage das ehrlich."
            : $"Du liest in diesem Kanal nur mit und mischst dich NICHT ins Gespräch ein. Antworte ausschließlich dann, wenn die Nachricht eine klare Sachfrage ist, die dir (oder allgemein) gestellt wird und die du mit den Wissensquellen fundiert beantworten kannst. Bei Aussagen, Meinungen, Aufrufen an die Allianz oder an andere Mitglieder, Begrüßungen, Smalltalk, Reaktionen oder allem, was keine an dich gerichtete, beantwortbare Sachfrage ist, antworte ausschließlich mit exakt {NoAnswerSentinel} und sonst nichts. Im Zweifel immer {NoAnswerSentinel}.");

        return sb.ToString();
    }

    // The grounding block: the messages from the guild's knowledge index most relevant to the
    // question (full-text search). Falls back to a live gather only while the index has no content
    // for the guild yet (before the first backfill), so early questions still work.
    private async Task<string> BuildKnowledgeBlockAsync(ulong guildId, string questionText, int knowledgeSnippetLimit, CancellationToken cancellationToken)
    {
        if (!await indexService.HasIndexedContentAsync(guildId, cancellationToken))
            return await indexService.GetRecentKnowledgeFallbackAsync(guildId, cancellationToken);

        var language = await ResolveSearchLanguageAsync(guildId);
        var hits = await indexService.SearchAsync(guildId, language, questionText, knowledgeSnippetLimit, cancellationToken);

        var sb = new StringBuilder();
        foreach (var hit in hits)
            sb.AppendLine(hit.ChannelId != 0 ? $"- [<#{hit.ChannelId}>] {hit.Content}" : $"- {hit.Content}");

        return sb.ToString();
    }

    // The guild's configured chat backend: the explicit Provider setting parsed to AiProvider
    // (default Gemini on unset/unknown), matched against the registered providers.
    private async Task<IAiChatProvider> ResolveProviderAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.Provider);
        var kind = Enum.TryParse<AiProvider>(configured, ignoreCase: true, out var parsed) ? parsed : AiProvider.Gemini;
        return providers.First(p => p.Kind == kind);
    }

    // Outcome of the passive-listening gate. Only No suppresses; the rest fall through to the main
    // model (Failed = the gate call errored/returned nothing, so we degrade to today's behaviour).
    private enum GateResult { Yes, No, Ambiguous, Failed }

    // The gate model for this guild: the explicit GateModel setting (the literal "off" disables the
    // gate), else the provider's default gate model (null when the provider has none — e.g. Ollama
    // with no Ollama:GateModel configured). Null ⇒ no gate, current behaviour.
    private async Task<string?> ResolveGateModelAsync(ulong guildId, IAiChatProvider provider)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.GateModel);
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

    // Per-guild FTS config: the explicit setting, else derived from the guild's Discord locale,
    // else "simple". Always normalized against the supported whitelist before use.
    private async Task<string> ResolveSearchLanguageAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.SearchLanguage);
        if (!string.IsNullOrWhiteSpace(configured))
            return FtsLanguage.Normalize(configured);

        var locale = gatewayClient.Cache.Guilds.GetValueOrDefault(guildId)?.PreferredLocale;
        return FtsLanguage.FromDiscordLocale(locale);
    }

    private static bool MentionsBotByName(string content, string botName)
    {
        foreach (var token in NonWord().Split(botName))
        {
            if (token.Length < 3)
                continue;
            if (Regex.IsMatch(content, $@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private static string Truncate(string text) =>
        text.Length <= DiscordMessageLimit ? text : text[..(DiscordMessageLimit - 1)] + "…";

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWord();

    // Gate-verdict tokens, matched as whole words on the upper-cased answer (JA/NEIN included in
    // case a model answers in German despite the one-word YES/NO instruction).
    [GeneratedRegex(@"\b(NO|NEIN)\b")]
    private static partial Regex GateNo();

    [GeneratedRegex(@"\b(YES|JA)\b")]
    private static partial Regex GateYes();
}
