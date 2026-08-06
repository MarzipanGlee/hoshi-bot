using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HoshiBot.Data;
using HoshiBot.Discord.TerritoryCapture;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;

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
//
// Partials: AiChatService.Context.cs builds the system instruction + grounding blocks,
// AiChatService.Answer.cs renders/streams the final reply text, AiChatService.Routing.cs resolves
// backend/settings and runs the gate/complexity classifiers, AiChatService.Compose.cs is the
// admin-driven "/hoshi-say" composer.
public partial class AiChatService(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureChannelService channelService,
    GuildFeatureSettingsService settingsService,
    EmbedBranding embedBranding,
    IEnumerable<IAiChatProvider> providers,
    AiChatIndexService indexService,
    TerritoryCaptureDigestService territoryCaptureDigest,
    MemberNoteService memberNoteService,
    MemoryService memoryService,
    AiChatEmbeddingService embeddingService,
    GuildAllianceService allianceService,
    AiChatHealthService healthService,
    LanguageResolver languageResolver,
    ILogger<AiChatService> logger)
{
    private const string NoAnswerSentinel = "[NO_ANSWER]";

    // The AI backend settings (provider, API key, models, embeddings) are guild-wide — one account
    // per guild — so they live under the separate AiBackend feature at the Guild scope, read the
    // same way regardless of which audience a message belongs to.
    private const GuildFeature BackendFeature = GuildFeature.AiBackend;
    private const GuildAudience BackendScope = GuildAudience.Guild;

    // The per-audience behavioral settings (system prompt, search language, memory toggle,
    // streaming) live under GuildFeature.AiChat at the audience the current message belongs to.
    // AiChat's channels are keyed per audience only (no alliance dimension), so we resolve the
    // audience from which enabled audience's listen/knowledge channels contain the message's
    // channel; for the Alliance audience the specific alliance can't be derived from the channel, so
    // we fall back to the guild's primary linked alliance (the house pattern — see
    // GuildAllianceService.GetPrimaryIdAsync; exact for single-alliance guilds, primary-wins for a
    // coalition guild). Resolved once per message in TryBuildReplyAsync. Safe as an instance field:
    // AiChatService is instantiated per message (see AiChatMessageHandler's per-message scope).
    private readonly record struct SettingsScope(GuildAudience Audience, int? AllianceId);
    private SettingsScope _settingsScope;

    // The language a public AiChat reply speaks: the CHANNEL's owning scope's language (not the
    // message author's — the whole channel reads the answer), resolved from _settingsScope via
    // ResolveReplyLanguageAsync. Drives the prompt's answer-language instruction, the prompt's
    // date/weekday rendering and the canned Persona replies. Same per-message instance-field
    // pattern as _settingsScope.
    private Language _replyLanguage;

    // Serializes AI answers per channel: only one generation runs at a time (a burst can't fire
    // overlapping, billable / CPU-thrashing generations), but a message that arrives while the
    // channel is busy WAITS its turn instead of being silently dropped. Total in-flight (the active
    // one + a small queue) is capped at MaxInFlightPerChannel so a chatter flood still can't pile up
    // a backlog; a message queued longer than MaxQueueWait is treated as stale and skipped.
    private sealed class ChannelSlot
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public int InFlight;
    }

    private static readonly ConcurrentDictionary<ulong, ChannelSlot> ChannelSlots = new();
    private const int MaxInFlightPerChannel = 3; // one generating + up to two queued
    private static readonly TimeSpan MaxQueueWait = TimeSpan.FromSeconds(90);

    // The reply text plus the exact set of user ids the reply is allowed to actually ping — the
    // conversation participants we told the model about. Discord's allowed_mentions is set to just
    // these, so a hallucinated or unknown <@id> in the text can never ping a random member.
    public readonly record struct AiChatReply(string Text, IReadOnlyList<ulong> AllowedUserIds);

    // Returns the reply to post, or null to stay silent. For a directly-addressed message, if
    // `onPartial` is supplied it's called with the answer-so-far as it streams in (throttled), so the
    // caller can post a placeholder and edit it live; the final returned reply is the authoritative
    // text for a last edit. Passive messages never stream (they may end in [NO_ANSWER] silence).
    public async Task<AiChatReply?> TryBuildReplyAsync(Message message, Func<string, ValueTask>? onPartial, CancellationToken cancellationToken)
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
        var mentionsOthers = message.MentionEveryone
            || message.MentionedRoleIds.Count > 0
            || message.MentionedUsers.Any(u => u.Id != botId);
        if (!addressed && mentionsOthers)
            return null;

        // Resolve which audience's tab the per-audience behavioral settings (system prompt, search
        // language, memory, streaming) come from for this message. Backend settings are guild-wide
        // and don't use this. Set once here, before any behavioral-setting read below.
        _settingsScope = await ResolveSettingsScopeAsync(guildId, message.ChannelId);
        _replyLanguage = await ResolveReplyLanguageAsync(guildId, _settingsScope);

        var provider = await ResolveProviderAsync(guildId);
        var apiKey = await settingsService.GetSecretAsync(guildId, BackendFeature, BackendScope, null, AiBackendSettingKeys.ApiKey);

        // Only Gemini authenticates per guild — the shared local Ollama needs no key.
        if (provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("AiChat enabled for guild {GuildId} but no Gemini API key is configured; staying silent.", guildId);
            return null;
        }

        var slot = ChannelSlots.GetOrAdd(message.ChannelId, static _ => new ChannelSlot());

        // Cap total in-flight (active + queued) per channel: over the cap we drop (chatter-flood
        // guard); under it we wait our turn rather than dropping a message that just happened to
        // arrive mid-answer.
        if (Interlocked.Increment(ref slot.InFlight) > MaxInFlightPerChannel)
        {
            Interlocked.Decrement(ref slot.InFlight);
            return null;
        }

        try
        {
            // Wait our turn — only one generation per channel at a time. Give up if we've been
            // queued long enough that the question is stale (a slow generation ahead of us).
            if (!await slot.Gate.WaitAsync(MaxQueueWait, cancellationToken))
            {
                logger.LogInformation(
                    "AiChat guild {Guild} ch {Channel}: dropped a queued message (waited > {WaitSeconds}s behind a slow answer)",
                    guildId, message.ChannelId, MaxQueueWait.TotalSeconds);
                return null;
            }

            try
            {
                // Recent conversational context (also reused for turns/mentionable below), fetched
                // before the gate so we can detect an active back-and-forth: if the message right
                // before this one is the bot's own — and this message isn't rallying other members —
                // it's a continuation of a conversation Hoshi is already in, so she keeps engaging
                // without needing a fresh @mention or a standalone question. Without this, a passive
                // banter follow-up right after Hoshi spoke was dropped by the gate (observed live).
                var history = await indexService.FetchRecentAsync(message.ChannelId, provider.HistoryLimit, cancellationToken) ?? [];
                history.Reverse(); // chronological
                var botSpokeBefore = history.Any(m => m.Author.Id == botId && m.Id != message.Id);

                if (!addressed && !mentionsOthers
                    && history.LastOrDefault(m => m.Id != message.Id) is { } prior && prior.Author.Id == botId)
                {
                    addressed = true; // conversational continuation: Hoshi just spoke, keep the thread going
                }

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

                var model = await settingsService.GetTextAsync(guildId, BackendFeature, BackendScope, null, AiBackendSettingKeys.Model);
                model = string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model.Trim();

                // Complexity routing (opt-in): a cheap classifier picks the answer model — simple
                // questions are answered by the cheap router model, complex ones by the main model
                // above. Errs to SIMPLE (see EvaluateComplexityAsync), so the premium model's scarce
                // quota (e.g. Gemini flash's 20/day) is only spent when the question clearly needs it.
                var routeLabel = "off";
                var routerModel = await ResolveRouterModelAsync(guildId);
                if (routerModel is not null)
                {
                    var complexity = await EvaluateComplexityAsync(routerModel, provider, apiKey, message.Author, content, cancellationToken);
                    routeLabel = complexity.ToString().ToLowerInvariant();
                    if (complexity == Complexity.Simple)
                        model = routerModel;
                }

                var systemExtra = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, _settingsScope.Audience, _settingsScope.AllianceId, AiChatSettingKeys.SystemPrompt);

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

                var systemInstruction = await BuildSystemInstructionAsync(guildId, message.ChannelId, botName, systemExtra, addressed, content, mentionable, provider.KnowledgeSnippetLimit, model, provider.Kind, cancellationToken);

                var request = new AiChatCompletionRequest(model, systemInstruction, turns, apiKey);
                string? answer;
                var overloaded = false;

                var streaming = onPartial is not null && await IsStreamingEnabledAsync(guildId);
                if (streaming)
                {
                    // Stream the answer so a long (CPU-only) generation appears live instead of a minute
                    // of "typing" then a wall of text. Opt-in per guild (AiChatSettingKeys.StreamResponses).
                    // Works for both addressed and passive (gate=yes) messages — StreamAnswerAsync handles
                    // the difference (an addressed message always answers, so it shows an instant
                    // placeholder; a passive one may still end in [NO_ANSWER] silence, so it bridges with
                    // the typing indicator and only posts once real content streams in).
                    answer = await StreamAnswerAsync(provider, request, onPartial!, addressed, message.ChannelId, botSpokeBefore, message.Author, botName, cancellationToken);
                }
                else
                {
                    // No streaming sink (no interactive caller): keep the typing indicator alive for the
                    // whole (potentially minute-long) generation, then stop it as soon as we have an answer.
                    using var typingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var typing = KeepTypingAsync(message.ChannelId, typingCts.Token);
                    try
                    {
                        var gen = await provider.GenerateDetailedAsync(request, cancellationToken);
                        answer = gen.Text;
                        overloaded |= gen.Failure == AiChatFailureKind.Overloaded;
                    }
                    finally
                    {
                        await typingCts.CancelAsync();
                        try { await typing; } catch (OperationCanceledException) { /* expected on stop */ }
                    }
                }

                // Resilience: an addressed question must get an answer. If the (possibly premium) main
                // model returned nothing — a real failure mode seen live: gemini-3.5-flash timing out or
                // "experiencing high demand" while flash-lite stays healthy — retry once on the lighter
                // model before falling back to the "can't answer" message. Quick, non-streaming.
                if (answer is null && addressed
                    && provider.DefaultGateModel is { } fallbackModel
                    && !string.Equals(model, fallbackModel, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation(
                        "AiChat guild {Guild} ch {Channel}: main model {Model} returned nothing; retrying on {Fallback}.",
                        guildId, message.ChannelId, model, fallbackModel);
                    var gen = await provider.GenerateDetailedAsync(request with { Model = fallbackModel }, cancellationToken);
                    answer = gen.Text;
                    overloaded |= gen.Failure == AiChatFailureKind.Overloaded;
                    if (answer is not null)
                        model = fallbackModel;
                }

                // Record chat backend health (main answer generation) so an outage/overload is
                // visible on the Web admin health page. A non-null answer — including the [NO_ANSWER]
                // sentinel — means the model responded (success); null means the generation failed.
                if (answer is not null)
                    await healthService.RecordSuccessAsync(guildId, AiChatProviderCallKind.Chat, model, cancellationToken);
                else
                    await healthService.RecordErrorAsync(guildId, AiChatProviderCallKind.Chat, model,
                        overloaded ? "Model overloaded / timed out" : "Generation returned no text", cancellationToken);

                // Both the main model and the flash-lite failover came up empty because of a transient
                // overload/timeout (not a genuine "no answer") → give a friendly in-character "busy"
                // reply that invites a retry, instead of the flat "kann ich leider nicht beantworten".
                var replyText = answer is null && addressed && overloaded
                    ? HoshiPersona.BusyReply(_replyLanguage)
                    : FinalizeAnswer(answer, addressed, botSpokeBefore, message.Author, botName);

                // One line per handled message so a "why did it stay silent / only give the fallback"
                // question is answerable straight from the logs.
                logger.LogInformation(
                    "AiChat guild {Guild} ch {Channel}: addressed={Addressed} inListen={InListen} gate={Gate} route={Route} turns={Turns} provider={Provider} model={Model} → answer={AnswerChars} reply={Reply}",
                    guildId, message.ChannelId, addressed, inListenChannel, gateLabel, routeLabel, turns.Count, provider.Kind, model,
                    answer?.Length.ToString() ?? "null", replyText is null ? "silent" : "posted");

                return replyText is null ? null : new AiChatReply(replyText, mentionable.Keys.ToList());
            }
            finally
            {
                slot.Gate.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref slot.InFlight);
        }
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

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWord();
}
