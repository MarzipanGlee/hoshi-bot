using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HoshiBot.Data;
using HoshiBot.Domain;
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
    TerritoryCaptureDigestService territoryCaptureDigest,
    MemberNoteService memberNoteService,
    MemoryService memoryService,
    AiChatEmbeddingService embeddingService,
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

    // Discord's typing indicator lasts ~10s; re-trigger a bit before that so it stays visible
    // across a slow (CPU-only Ollama) generation instead of stopping mid-wait.
    private static readonly TimeSpan TypingRefreshInterval = TimeSpan.FromSeconds(8);

    // How often a streamed answer's message is edited with the text-so-far. Discord rate-limits
    // message edits, so coalesce the token stream to at most one edit per this interval.
    private const int StreamEditIntervalMs = 1250;

    // The three scalar settings (API key, system prompt, model) plus the search language are
    // guild-wide — one Gemini account per guild — so they live at the None/null scope regardless of
    // which audiences the feature is enabled for (same pattern as ClientRelease's guild-wide
    // platform roles). The channel lists, by contrast, are per-audience.
    private const GuildAudience SettingsScope = GuildAudience.None;

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

        var provider = await ResolveProviderAsync(guildId);
        var apiKey = await settingsService.GetSecretAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.ApiKey);

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
                var history = await indexService.FetchRecentAsync(message.ChannelId, provider.HistoryLimit, cancellationToken);
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

                var model = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.Model);
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

                var systemExtra = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.SystemPrompt);

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

                var systemInstruction = await BuildSystemInstructionAsync(guildId, message.ChannelId, botName, systemExtra, addressed, content, mentionable, provider.KnowledgeSnippetLimit, cancellationToken);

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

                // Both the main model and the flash-lite failover came up empty because of a transient
                // overload/timeout (not a genuine "no answer") → give a friendly in-character "busy"
                // reply that invites a retry, instead of the flat "kann ich leider nicht beantworten".
                var replyText = answer is null && addressed && overloaded
                    ? HoshiPersona.BusyReply()
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

    // Streams the answer, pushing the text-so-far to `onPartial` at most every StreamEditIntervalMs
    // (Discord edit-rate friendly). Returns the raw accumulated answer; the caller applies the
    // authoritative FinalizeAnswer to it for the last edit.
    //
    // Addressed vs passive: an addressed message always answers, so it posts an instant "…" placeholder
    // for immediate feedback. A passive (gate=yes) message can still resolve to [NO_ANSWER] silence, so
    // it must NOT post upfront — instead it runs the typing indicator to bridge the (CPU-only)
    // prompt-eval gap and only posts once RenderStreamedPartial yields real content (never for a
    // still-empty/[NO_ANSWER] buffer), so a punted passive answer stays silent with no orphaned message.
    private async Task<string?> StreamAnswerAsync(IAiChatProvider provider, AiChatCompletionRequest request,
        Func<string, ValueTask> onPartial, bool addressed, ulong channelId, bool botSpokeBefore, NetCord.User author, string botName, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var sinceEdit = Stopwatch.StartNew();

        using var typingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var typing = Task.CompletedTask;
        if (addressed)
            await onPartial("…");
        else
            typing = KeepTypingAsync(channelId, typingCts.Token);

        try
        {
            await foreach (var delta in provider.GenerateStreamAsync(request, cancellationToken))
            {
                sb.Append(delta);
                if (sinceEdit.ElapsedMilliseconds < StreamEditIntervalMs)
                    continue;

                var partial = RenderStreamedPartial(sb.ToString(), botSpokeBefore, author, botName);
                if (partial is not null)
                {
                    if (!typingCts.IsCancellationRequested)
                        await typingCts.CancelAsync(); // real content is up — stop the bridge typing
                    await onPartial(partial);
                }
                sinceEdit.Restart();
            }
        }
        finally
        {
            if (!typingCts.IsCancellationRequested)
                await typingCts.CancelAsync();
            try { await typing; } catch (OperationCanceledException) { /* expected on stop */ }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    // Live-partial rendering: like FinalizeAnswer but returns null (⇒ don't post/edit) whenever there's
    // no real content yet — so a passive stream that's still just an empty/[NO_ANSWER] buffer never
    // posts a premature message, and an addressed placeholder isn't blanked mid-stream.
    private static string? RenderStreamedPartial(string raw, bool botSpokeBefore, NetCord.User author, string botName)
    {
        var text = raw.Replace(NoAnswerSentinel, "", StringComparison.OrdinalIgnoreCase).Trim();
        text = StripSelfNamePrefix(text, botName);

        // Drop the trailing (likely half-streamed) word so the live edits only ever show whole words,
        // not "Gebiets" → "Gebietsübernahme". The final edit uses the complete answer, so nothing is
        // lost. Until a word boundary exists we post nothing yet.
        var lastBoundary = text.LastIndexOfAny([' ', '\n', '\t']);
        if (lastBoundary <= 0)
            return null;
        text = text[..lastBoundary].TrimEnd();
        if (text.Length == 0)
            return null;

        if (!botSpokeBefore && !text.StartsWith("Commander", StringComparison.OrdinalIgnoreCase))
            text = CommanderName.Greeting(author) + text;

        return Truncate(text);
    }

    private async Task<string> BuildSystemInstructionAsync(ulong guildId, ulong channelId, string botName, string? systemExtra, bool addressed, string questionText, IReadOnlyDictionary<ulong, string> mentionable, int knowledgeSnippetLimit, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine(HoshiPersona.Describe(botName));
        sb.AppendLine("Antworte auf Deutsch, freundlich und knapp. Nutze zum Beantworten in erster Linie die unten angegebenen verlässlichen Fakten, Wissensquellen und den bisherigen Chatverlauf.");

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

        var conversationMemory = await BuildConversationMemoryBlockAsync(guildId, channelId, cancellationToken);
        if (conversationMemory.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Bisheriger Gesprächsverlauf in diesem Kanal (Zusammenfassungen älterer Nachrichten; die neuesten Nachrichten stehen bereits oben im Verlauf):");
            sb.Append(conversationMemory);
        }

        var memberNotes = await BuildMemberNotesAsync(guildId, cancellationToken);
        if (memberNotes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Was du über die Mitglieder weißt (nutze das für warme, insider-artige Anspielungen und um dich wie ein echtes Community-Mitglied zu verhalten; du darfst auch über gerade abwesende Mitglieder reden; wenn etwas unpassend wäre, lass es einfach weg):");
            sb.Append(memberNotes);
        }

        var memories = await BuildMemoryBlockAsync(guildId, questionText, cancellationToken);
        if (memories.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Woran du dich aus dem Leben der Community erinnerst (nutze es für passende Anspielungen auf frühere Ereignisse; wenn es nicht zum Gespräch passt, lass es weg):");
            sb.Append(memories);
        }

        var facts = await BuildTerritoryCaptureFactsAsync(guildId, cancellationToken);
        if (facts.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Verlässliche Fakten aus der Datenbank (bevorzuge diese vor allgemeinem Wissen und vor unsicheren Chat-Auszügen). Gib Zeitangaben in der Form <t:UNIX:t> unverändert weiter — Discord zeigt sie dann automatisch in der Lokalzeit jedes Nutzers:");
            sb.Append(facts);
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

        // Trace what retrieval actually surfaced, so "why didn't she know X?" is answerable from logs
        // (the top hit channels + snippet heads for this question).
        logger.LogInformation("AiChat knowledge hits guild {Guild} q=\"{Question}\": [{Hits}]",
            guildId, questionText,
            string.Join(" | ", hits.Select(h => $"{h.ChannelName}: {h.Content[..Math.Min(50, h.Content.Length)].Replace('\n', ' ')}")));

        var sb = new StringBuilder();
        foreach (var hit in hits)
            sb.AppendLine(hit.ChannelId != 0 ? $"- [<#{hit.ChannelId}>] {hit.Content}" : $"- {hit.Content}");

        return sb.ToString();
    }

    // The community-lore block: one compact line per member with a GuildMemberNote, so Hoshi behaves
    // like a real member who knows the whole cast — including people not currently in the conversation
    // (the "ensemble" effect). Only the live, approved fields inject; suggestions never do, and a
    // member's peer-lore veto (PeerLoreHidden) drops the peer fields. Gated on the MemberLore feature
    // and capped by a character budget so a big roster can't blow up the prompt. See docs/ai-chat-member-lore.md.
    private const int MemberNotesCharBudget = 4000;

    private async Task<string> BuildMemberNotesAsync(ulong guildId, CancellationToken cancellationToken)
    {
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.MemberLore))
            return "";

        var notes = await memberNoteService.GetForGuildAsync(guildId, cancellationToken);
        if (notes.Count == 0)
            return "";

        // Consolidate a person's accounts (player-linked alts share a person key) into one entry so
        // Hoshi treats them as the same commander and uses lore stored on either account.
        var personKeys = await memberNoteService.GetPersonKeysAsync(notes.Select(n => n.DiscordUserId), cancellationToken);

        var sb = new StringBuilder();
        foreach (var group in notes.GroupBy(n => personKeys.GetValueOrDefault(n.DiscordUserId, $"user:{n.DiscordUserId}")))
        {
            var ids = group
                .OrderByDescending(n => string.IsNullOrWhiteSpace(n.PreferredName) ? 0 : 1)
                .ThenBy(n => n.DiscordUserId)
                .Select(n => n.DiscordUserId)
                .ToList();
            var line = RenderPersonNote(ids, MergePersonNotes(group));
            if (line is null)
                continue;
            if (sb.Length + line.Length > MemberNotesCharBudget)
                break;
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    // Combine a person's per-account notes into one view: first non-empty preferred name, distinct
    // values per field, and peer fields dropped if *any* of the person's accounts vetoed them.
    private static GuildMemberNote MergePersonNotes(IEnumerable<GuildMemberNote> group)
    {
        var list = group.ToList();
        var peerHidden = list.Any(n => n.PeerLoreHidden);
        return new GuildMemberNote
        {
            PreferredName = list.Select(n => n.PreferredName).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
            Nicknames = MergeField(list.Select(n => n.Nicknames)),
            Interests = MergeField(list.Select(n => n.Interests)),
            Background = MergeField(list.Select(n => n.Background)),
            Languages = MergeField(list.Select(n => n.Languages)),
            RunningJokes = peerHidden ? null : MergeField(list.Select(n => n.RunningJokes)),
            TeaseAbout = peerHidden ? null : MergeField(list.Select(n => n.TeaseAbout)),
        };
    }

    private static string? MergeField(IEnumerable<string?> values)
    {
        var parts = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Inline(v!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    // One compact line for a person, listing all their account mentions so the bot can ping either.
    // Fields are already merged + peer-veto-applied by MergePersonNotes.
    private static string? RenderPersonNote(IReadOnlyList<ulong> ids, GuildMemberNote note)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(note.Nicknames)) parts.Add($"auch {note.Nicknames}");
        if (!string.IsNullOrWhiteSpace(note.Interests)) parts.Add($"mag {note.Interests}");
        if (!string.IsNullOrWhiteSpace(note.Background)) parts.Add(note.Background);
        if (!string.IsNullOrWhiteSpace(note.Languages)) parts.Add($"spricht {note.Languages}");
        if (!string.IsNullOrWhiteSpace(note.RunningJokes)) parts.Add($"Running Gags: {note.RunningJokes}");
        if (!string.IsNullOrWhiteSpace(note.TeaseAbout)) parts.Add($"necken ok: {note.TeaseAbout}");

        if (parts.Count == 0)
            return null;

        var name = string.IsNullOrWhiteSpace(note.PreferredName) ? "" : $"{note.PreferredName.Trim()} ";
        var mentions = string.Join(", ", ids.Select(id => $"<@{id}>"));
        return $"- {name}({mentions}): {string.Join("; ", parts)}";
    }

    // Flatten a multi-line note field (fields accumulate appended lines) into one line for the compact block.
    private static string Inline(string value) => value.Replace("\r", "").Replace("\n", "; ").Trim();

    // Hoshi's episodic memory (Phase 1): the community events she's formed and can recall. Retrieves
    // the ones most relevant to the current question (semantic search) plus a few recent + salient
    // ones, so she can reference past happenings like a real member. Reinforces what it surfaces (so
    // used memories resist decay). Gated on AiChatSettingKeys.MemoryEnabled. See the memory plan.
    private const int MemoryCharBudget = 3000;
    private const int MemoryRelevantLimit = 6;
    private const int MemoryRecentLimit = 3;

    private async Task<string> BuildMemoryBlockAsync(ulong guildId, string questionText, CancellationToken cancellationToken)
    {
        var enabled = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryEnabled);
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            return "";

        var memories = new List<GuildMemory>();
        if (embeddingService.Enabled && !string.IsNullOrWhiteSpace(questionText)
            && await embeddingService.EmbedAsync(questionText, cancellationToken) is { } queryVec)
        {
            memories.AddRange(await memoryService.SearchEpisodicAsync(guildId, queryVec, MemoryRelevantLimit, cancellationToken));
        }

        // Always fold in a few recent + salient ones so standout events surface even when the current
        // question doesn't match them semantically.
        foreach (var recent in await memoryService.GetRecentSalientAsync(guildId, MemoryScope.Episodic, MemoryRecentLimit, cancellationToken))
            if (memories.All(m => m.Id != recent.Id))
                memories.Add(recent);

        if (memories.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var memory in memories)
        {
            var line = $"- ({memory.CreatedAt:yyyy-MM-dd}) {Inline(memory.Content)}";
            if (sb.Length + line.Length > MemoryCharBudget)
                break;
            sb.AppendLine(line);
        }

        // Reinforce what we actually surfaced so genuinely useful memories resist decay.
        await memoryService.ReinforceAsync(memories.Select(m => m.Id), cancellationToken);

        return sb.ToString();
    }

    // Hoshi's longer conversation memory (Phase 2): the current channel's recent conversation snapshots
    // — summaries of what was discussed here before the messages that are still in the live window — so
    // a thread survives past the 15-message window. Recency-scoped to this channel; gated on MemoryEnabled.
    private const int ConversationSnapshotLimit = 4;

    private async Task<string> BuildConversationMemoryBlockAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken)
    {
        var enabled = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.MemoryEnabled);
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            return "";

        var snapshots = await memoryService.GetRecentConversationAsync(guildId, channelId, ConversationSnapshotLimit, cancellationToken);
        if (snapshots.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var snapshot in snapshots)
            sb.AppendLine($"- ({snapshot.CreatedAt:yyyy-MM-dd HH:mm}) {Inline(snapshot.Content)}");

        await memoryService.ReinforceAsync(snapshots.Select(m => m.Id), cancellationToken);
        return sb.ToString();
    }

    // Structured, authoritative grounding straight from the DB (not fuzzy chat snippets): this week's
    // Territory Capture zones for the guild's TC-enabled alliances — owned zone, tier and capture
    // window — so the bot can answer "which zones do we hold / when is the next capture?" directly
    // instead of deferring to the digest channel. Times are pre-rendered as Discord timestamps so the
    // model can relay them verbatim and Discord localizes them for each reader. Empty when the guild
    // runs no Territory Capture alliances (then this block is simply omitted).
    private async Task<string> BuildTerritoryCaptureFactsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var links = await territoryCaptureDigest.GetTcEnabledLinksAsync(guildId);
        if (links.Count == 0)
            return "";

        var weekStart = TerritoryCaptureScheduler.GetWeekStart(DateTimeOffset.UtcNow);
        var german = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder();

        foreach (var link in links)
        {
            var slots = await territoryCaptureDigest.GetWeeklySlotAssignmentsAsync(link.StfcAllianceId, weekStart);
            if (slots.Count == 0)
                continue;

            sb.AppendLine($"Gebietsübernahmen dieser Woche für die Allianz [{link.StfcAlliance.Tag}]:");
            foreach (var (_, territory, start, end) in slots)
            {
                var day = start.ToString("dddd", german);
                sb.AppendLine($"- {territory.Name} (Tier {territory.Tier}): {day}, <t:{start.ToUnixTimeSeconds()}:t>–<t:{end.ToUnixTimeSeconds()}:t>");
            }
        }

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

    // Opt-in per guild: answers stream (placeholder/typing → live edits) only when StreamResponses is
    // "true". Unset (default) → classic post-once.
    private async Task<bool> IsStreamingEnabledAsync(ulong guildId)
    {
        var value = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.StreamResponses);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private enum Complexity { Simple, Complex }

    // The complexity-router model for this guild, or null when routing is off (RouterModel unset or
    // "off"). Opt-in: no provider fallback, so an existing guild's behaviour is unchanged until set.
    private async Task<string?> ResolveRouterModelAsync(ulong guildId)
    {
        var configured = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.RouterModel);
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

    // Complexity-router verdict tokens, matched as whole words on the upper-cased answer.
    [GeneratedRegex(@"\bCOMPLEX\b")]
    private static partial Regex ComplexWord();

    [GeneratedRegex(@"\bSIMPLE\b")]
    private static partial Regex SimpleWord();
}
