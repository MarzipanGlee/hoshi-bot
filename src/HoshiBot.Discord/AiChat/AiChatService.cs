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
    GeminiClient gemini,
    AiChatIndexService indexService,
    ILogger<AiChatService> logger)
{
    private const string NoAnswerSentinel = "[NO_ANSWER]";
    private const int HistoryLimit = 15;
    private const int MaxKnowledgeSnippets = 12;
    private const int DiscordMessageLimit = 2000;

    // The three scalar settings (API key, system prompt, model) plus the search language are
    // guild-wide — one Gemini account per guild — so they live at the None/null scope regardless of
    // which audiences the feature is enabled for (same pattern as ClientRelease's guild-wide
    // platform roles). The channel lists, by contrast, are per-audience.
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

            var history = await indexService.FetchRecentAsync(message.ChannelId, HistoryLimit, cancellationToken);
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
                var text = AiChatIndexService.RenderMessageText(m);
                if (string.IsNullOrEmpty(text))
                    continue;
                turns.Add(m.Author.Id == botId
                    ? new GeminiClient.Turn("model", text)
                    : new GeminiClient.Turn("user", $"{CommanderName.Of(m.Author)}: {text}"));
            }

            turns.Add(new GeminiClient.Turn("user", $"{CommanderName.Of(message.Author)}: {content}"));

            var systemInstruction = await BuildSystemInstructionAsync(guildId, botName, systemExtra, addressed, content, cancellationToken);

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

    private async Task<string> BuildSystemInstructionAsync(ulong guildId, string botName, string? systemExtra, bool addressed, string questionText, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Du bist {botName}, ein hilfreicher Assistent für diese Discord-Community (ein Star-Trek-Fleet-Command-Allianz-Server).");
        sb.AppendLine("Antworte auf Deutsch, freundlich und knapp. Nutze zum Beantworten in erster Linie die unten angegebenen Wissensquellen und den bisherigen Chatverlauf.");

        if (!string.IsNullOrWhiteSpace(systemExtra))
        {
            sb.AppendLine();
            sb.AppendLine(systemExtra.Trim());
        }

        var knowledge = await BuildKnowledgeBlockAsync(guildId, questionText, cancellationToken);
        if (knowledge.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Wissensquellen (relevante Auszüge aus den konfigurierten Kanälen):");
            sb.Append(knowledge);
        }

        sb.AppendLine();
        sb.AppendLine(addressed
            ? "Du wirst in dieser Nachricht direkt angesprochen. Antworte immer. Wenn du etwas nicht sicher weißt, sage das ehrlich."
            : $"Antworte NUR, wenn du eine wirklich hilfreiche, fundierte Antwort geben kannst. Wenn die Nachricht keine beantwortbare Frage ist oder du keine fundierte Antwort hast, antworte ausschließlich mit exakt {NoAnswerSentinel} und sonst nichts.");

        return sb.ToString();
    }

    // The grounding block: the messages from the guild's knowledge index most relevant to the
    // question (full-text search). Falls back to a live gather only while the index has no content
    // for the guild yet (before the first backfill), so early questions still work.
    private async Task<string> BuildKnowledgeBlockAsync(ulong guildId, string questionText, CancellationToken cancellationToken)
    {
        if (!await indexService.HasIndexedContentAsync(guildId, cancellationToken))
            return await indexService.GetRecentKnowledgeFallbackAsync(guildId, cancellationToken);

        var language = await ResolveSearchLanguageAsync(guildId);
        var hits = await indexService.SearchAsync(guildId, language, questionText, MaxKnowledgeSnippets, cancellationToken);

        var sb = new StringBuilder();
        foreach (var hit in hits)
            sb.AppendLine(hit.ChannelName is null ? $"- {hit.Content}" : $"- [#{hit.ChannelName}] {hit.Content}");

        return sb.ToString();
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
}
