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
// memory) plus the configured knowledge channels as grounding, then calling Gemini. Returns null
// whenever the bot should stay silent, so the gateway handler stays a thin "reply if non-null".
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
    GeminiClient gemini,
    ILogger<AiChatService> logger)
{
    private const string NoAnswerSentinel = "[NO_ANSWER]";
    private const int HistoryLimit = 15;
    private const int KnowledgeLimitPerChannel = 20;
    private const int DiscordMessageLimit = 2000;

    // Bounds on knowledge gathering so a big forum / many channels can't explode the prompt (and
    // per-answer REST/Gemini cost): at most this many resolved sources, and this many archived
    // threads per forum.
    private const int MaxKnowledgeSources = 25;
    private const int ForumArchivedThreadLimit = 10;

    // The three scalar settings (API key, system prompt, model) are guild-wide — one Gemini
    // account per guild — so they live at the None/null scope regardless of which audiences the
    // feature is enabled for (same pattern as ClientRelease's guild-wide platform roles). The
    // channel lists, by contrast, are per-audience (see GetEnabledAudienceChannelsAsync).
    private const GuildAudience SettingsScope = GuildAudience.None;

    // One in-flight answer per channel — a passive listener could otherwise fire several
    // overlapping (and billable) Gemini calls for a burst of messages in the same channel.
    private static readonly ConcurrentDictionary<ulong, byte> InFlightChannels = new();

    // Returns the reply to post, or null to stay silent.
    public async Task<string?> TryBuildReplyAsync(Message message, CancellationToken cancellationToken)
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

        var apiKey = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.ApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("AiChat enabled for guild {GuildId} but no Gemini API key is configured; staying silent.", guildId);
            return null;
        }

        if (!InFlightChannels.TryAdd(message.ChannelId, 0))
            return null;

        try
        {
            var model = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.Model);
            model = string.IsNullOrWhiteSpace(model) ? GeminiClient.DefaultModel : model.Trim();

            var systemExtra = await settingsService.GetTextAsync(guildId, GuildFeature.AiChat, SettingsScope, null, AiChatSettingKeys.SystemPrompt);

            var history = await FetchRecentAsync(message.ChannelId, HistoryLimit, cancellationToken);
            history.Reverse(); // chronological
            var botSpokeBefore = history.Any(m => m.Author.Id == botId && m.Id != message.Id);

            // Prior context from the recent window (the short conversational memory), excluding the
            // triggering message — we append that ourselves below so the actual question is always
            // the final user turn even if the REST fetch hasn't caught up to it yet.
            var turns = new List<GeminiClient.Turn>();
            foreach (var m in history)
            {
                if (m.Id == message.Id)
                    continue;
                var text = RenderMessageText(m);
                if (string.IsNullOrEmpty(text))
                    continue;
                turns.Add(m.Author.Id == botId
                    ? new GeminiClient.Turn("model", text)
                    : new GeminiClient.Turn("user", $"{CommanderName.Of(m.Author)}: {text}"));
            }

            turns.Add(new GeminiClient.Turn("user", $"{CommanderName.Of(message.Author)}: {content}"));

            var systemInstruction = await BuildSystemInstructionAsync(guildId, botName, systemExtra, addressed, cancellationToken);

            // Show a typing indicator while the (slow) generation runs.
            try { await gatewayClient.Rest.TriggerTypingAsync(message.ChannelId, cancellationToken: cancellationToken); }
            catch (RestException) { /* non-fatal */ }

            var answer = await gemini.GenerateAsync(apiKey, model, systemInstruction, turns, cancellationToken);
            var reply = FinalizeAnswer(answer, addressed, botSpokeBefore, message.Author);

            // One line per handled message so a "why did it stay silent / only give the fallback"
            // question is answerable straight from the logs.
            logger.LogInformation(
                "AiChat guild {Guild} ch {Channel}: addressed={Addressed} inListen={InListen} turns={Turns} model={Model} → gemini={GeminiChars} reply={Reply}",
                guildId, message.ChannelId, addressed, inListenChannel, turns.Count, model,
                answer?.Length.ToString() ?? "null", reply is null ? "silent" : "posted");

            return reply;
        }
        finally
        {
            InFlightChannels.TryRemove(message.ChannelId, out _);
        }
    }

    private string? FinalizeAnswer(string? answer, bool addressed, bool botSpokeBefore, NetCord.User author)
    {
        if (answer is null)
            return addressed ? PolitelyUnsure(botSpokeBefore, author) : null;

        var punted = answer.Contains(NoAnswerSentinel, StringComparison.OrdinalIgnoreCase);
        answer = answer.Replace(NoAnswerSentinel, "", StringComparison.OrdinalIgnoreCase).Trim();

        if (punted || answer.Length == 0)
            return addressed ? PolitelyUnsure(botSpokeBefore, author) : null;

        // The first bot reply in a conversation opens with the "Commander {name}," convention.
        if (!botSpokeBefore && !answer.StartsWith("Commander", StringComparison.OrdinalIgnoreCase))
            answer = CommanderName.Greeting(author) + answer;

        return Truncate(answer);
    }

    // When the bot is addressed directly it must always say something, even if it has no real
    // answer — greet on the first turn just like a real reply.
    private static string PolitelyUnsure(bool botSpokeBefore, NetCord.User author)
    {
        const string body = "das kann ich dir leider nicht beantworten.";
        return botSpokeBefore ? char.ToUpper(body[0]) + body[1..] : CommanderName.Greeting(author) + body;
    }

    private async Task<string> BuildSystemInstructionAsync(ulong guildId, string botName, string? systemExtra, bool addressed, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Du bist {botName}, ein hilfreicher Assistent für diese Discord-Community (ein Star-Trek-Fleet-Command-Allianz-Server).");
        sb.AppendLine("Antworte auf Deutsch, freundlich und knapp. Nutze zum Beantworten in erster Linie die unten angegebenen Wissensquellen und den bisherigen Chatverlauf.");

        if (!string.IsNullOrWhiteSpace(systemExtra))
        {
            sb.AppendLine();
            sb.AppendLine(systemExtra.Trim());
        }

        var knowledge = await BuildKnowledgeBlockAsync(guildId, cancellationToken);
        if (knowledge.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Wissensquellen (aktuelle Auszüge aus den konfigurierten Kanälen):");
            sb.Append(knowledge);
        }

        sb.AppendLine();
        sb.AppendLine(addressed
            ? "Du wirst in dieser Nachricht direkt angesprochen. Antworte immer. Wenn du etwas nicht sicher weißt, sage das ehrlich."
            : $"Antworte NUR, wenn du eine wirklich hilfreiche, fundierte Antwort geben kannst. Wenn die Nachricht keine beantwortbare Frage ist oder du keine fundierte Antwort hast, antworte ausschließlich mit exakt {NoAnswerSentinel} und sonst nichts.");

        return sb.ToString();
    }

    private async Task<string> BuildKnowledgeBlockAsync(ulong guildId, CancellationToken cancellationToken)
    {
        // Knowledge channels are per-audience; gather them for every audience AiChat is enabled
        // for (AiChatKnowledge itself is never "enabled" — it's a storage-only channel bucket, so
        // we key it off AiChat's enabled audiences).
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);
        var configured = new List<ulong>();
        foreach (var audience in enabledAudiences)
            configured.AddRange(await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledge, audience));
        configured = configured.Distinct().ToList();
        if (configured.Count == 0)
            return "";

        var channels = await ExpandKnowledgeChannelsAsync(guildId, configured, cancellationToken);
        if (channels.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var channelId in channels.Take(MaxKnowledgeSources))
        {
            var messages = await FetchRecentAsync(channelId, KnowledgeLimitPerChannel, cancellationToken);
            messages.Reverse();
            foreach (var m in messages)
            {
                var text = RenderMessageText(m);
                if (!string.IsNullOrEmpty(text))
                    sb.AppendLine($"- {text}");
            }
        }

        return sb.ToString();
    }

    // Resolves the configured knowledge entries to concrete message-source ids (plain text
    // channels and forum THREADS — a forum channel itself holds no messages, its content lives in
    // its posts/threads). A category expands to every readable text channel and forum under it;
    // the bot silently reads what it can (FetchRecentAsync returns empty for anything it can't
    // access).
    private async Task<List<ulong>> ExpandKnowledgeChannelsAsync(ulong guildId, List<ulong> configured, CancellationToken cancellationToken)
    {
        IReadOnlyList<IGuildChannel> all;
        if (gatewayClient.Cache.Guilds.TryGetValue(guildId, out var cachedGuild) && cachedGuild.Channels.Count > 0)
        {
            all = cachedGuild.Channels.Values.ToList();
        }
        else
        {
            try { all = await gatewayClient.Rest.GetGuildChannelsAsync(guildId, cancellationToken: cancellationToken); }
            catch (RestException) { all = []; }
        }

        var byId = all.ToDictionary(c => c.Id);
        var resolved = new List<ulong>();
        foreach (var id in configured)
        {
            if (byId.GetValueOrDefault(id) is CategoryGuildChannel)
            {
                foreach (var child in all.Where(c => ParentIdOf(c) == id))
                    await AddSourceAsync(guildId, child, resolved, cancellationToken);
            }
            else if (byId.GetValueOrDefault(id) is { } channel)
            {
                await AddSourceAsync(guildId, channel, resolved, cancellationToken);
            }
            else
            {
                // Not in the guild's channel list (e.g. an already-thread id) — try it directly.
                resolved.Add(id);
            }
        }

        return resolved.Distinct().ToList();
    }

    // Adds one channel as a message source: a forum contributes its threads, a plain text channel
    // contributes itself, voice/stage contribute nothing.
    private async Task AddSourceAsync(ulong guildId, IGuildChannel channel, List<ulong> resolved, CancellationToken cancellationToken)
    {
        switch (channel)
        {
            case ForumGuildChannel:
                resolved.AddRange(await GetForumThreadIdsAsync(guildId, channel.Id, cancellationToken));
                break;
            case VoiceGuildChannel or StageGuildChannel:
                break;
            case TextGuildChannel:
                resolved.Add(channel.Id);
                break;
        }
    }

    // A forum's readable posts: its active threads plus a capped page of recently-archived ones.
    private async Task<List<ulong>> GetForumThreadIdsAsync(ulong guildId, ulong forumId, CancellationToken cancellationToken)
    {
        var threadIds = new List<ulong>();
        try
        {
            var active = await gatewayClient.Rest.GetActiveGuildThreadsAsync(guildId, cancellationToken: cancellationToken);
            threadIds.AddRange(active.Where(t => t.ParentId == forumId).Select(t => t.Id));
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not fetch active threads for forum {ForumId}", forumId);
        }

        try
        {
            var count = 0;
            await foreach (var thread in gatewayClient.Rest.GetPublicArchivedGuildThreadsAsync(forumId).WithCancellation(cancellationToken))
            {
                threadIds.Add(thread.Id);
                if (++count >= ForumArchivedThreadLimit)
                    break;
            }
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not fetch archived threads for forum {ForumId}", forumId);
        }

        return threadIds.Distinct().ToList();
    }

    private static ulong? ParentIdOf(IGuildChannel channel) => channel switch
    {
        TextGuildChannel t => t.ParentId,
        ForumGuildChannel f => f.ParentId,
        _ => null,
    };

    // A message's readable text: its content plus any embed text (title/description/fields/
    // footer/author) — many info channels (RoE, rules, announcements) post ONLY embeds, so
    // reading just Content would see nothing there.
    private static string RenderMessageText(RestMessage message)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(message.Content))
            sb.AppendLine(message.Content.Trim());

        foreach (var embed in message.Embeds)
        {
            if (!string.IsNullOrWhiteSpace(embed.Author?.Name))
                sb.AppendLine(embed.Author!.Name);
            if (!string.IsNullOrWhiteSpace(embed.Title))
                sb.AppendLine(embed.Title);
            if (!string.IsNullOrWhiteSpace(embed.Description))
                sb.AppendLine(embed.Description);
            foreach (var field in embed.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Name) || !string.IsNullOrWhiteSpace(field.Value))
                    sb.AppendLine($"{field.Name}: {field.Value}");
            }
            if (!string.IsNullOrWhiteSpace(embed.Footer?.Text))
                sb.AppendLine(embed.Footer!.Text);
        }

        return sb.ToString().Trim();
    }

    // Newest-first list of up to `limit` recent messages; empty on any REST error (missing
    // permissions etc. must never crash the message pump).
    private async Task<List<RestMessage>> FetchRecentAsync(ulong channelId, int limit, CancellationToken cancellationToken)
    {
        var messages = new List<RestMessage>();
        try
        {
            await foreach (var m in gatewayClient.Rest.GetMessagesAsync(channelId).WithCancellation(cancellationToken))
            {
                messages.Add(m);
                if (messages.Count >= limit)
                    break;
            }
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not fetch messages from channel {ChannelId}", channelId);
        }

        return messages;
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
}
