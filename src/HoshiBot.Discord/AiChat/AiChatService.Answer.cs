using System.Diagnostics;
using System.Text;
using HoshiBot.Domain;
using NetCord.Rest;

namespace HoshiBot.Discord.AiChat;

// Answer rendering: turning the model's raw output into the reply that actually gets posted —
// sentinel/self-prefix cleanup, the "Commander {name}," opener, the polite fallback for an
// addressed question with no answer, plus the streaming path (live partial edits) and the
// typing-indicator bridge for slow generations.
public partial class AiChatService
{
    private const int DiscordMessageLimit = 2000;

    // Discord's typing indicator lasts ~10s; re-trigger a bit before that so it stays visible
    // across a slow (CPU-only Ollama) generation instead of stopping mid-wait.
    private static readonly TimeSpan TypingRefreshInterval = TimeSpan.FromSeconds(8);

    // How often a streamed answer's message is edited with the text-so-far. Discord rate-limits
    // message edits, so coalesce the token stream to at most one edit per this interval.
    private const int StreamEditIntervalMs = 1250;

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

        return Truncate(DiscordMarkdown.NormalizeForPlainMessage(answer));
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

        // Normalise here too, not just in FinalizeAnswer — otherwise the live edits would show raw
        // "[text](url)" markdown that the final edit then silently rewrites. A half-streamed link
        // doesn't match yet and gets normalised on a later pass.
        return Truncate(DiscordMarkdown.NormalizeForPlainMessage(text));
    }

    private static string Truncate(string text) =>
        text.Length <= DiscordMessageLimit ? text : text[..(DiscordMessageLimit - 1)] + "…";
}
