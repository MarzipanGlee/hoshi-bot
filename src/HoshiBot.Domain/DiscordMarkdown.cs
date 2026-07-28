using System.Text.RegularExpressions;

namespace HoshiBot.Domain;

// Rewrites the markdown a language model likes to produce into what Discord actually renders
// in a PLAIN message (Content, not an embed). Discord only resolves masked links "[text](url)"
// inside embeds — in a normal message they show up verbatim, brackets and all, which is how
// Hoshi once answered with a raw "[https://discord.com/channels/…](https://discord.com/channels/…)".
// Embed-rendering code paths (the announcement forwarder) must NOT run through this.
public static partial class DiscordMarkdown
{
    // A channel URL — no message id at the end. That id-less form is exactly what "<#id>" means,
    // so it can be turned into a real channel mention; with a message id it's a jump link to one
    // specific message, which a mention cannot express (kept as-is; Discord links bare URLs).
    [GeneratedRegex(@"https?://(?:\w+\.)?discord(?:app)?\.com/channels/\d+/(\d+)(?![/\d])", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelUrlRegex();

    // Masked link whose target is such a channel URL. Matched before the generic link rule so the
    // link text can be dropped — the "<#id>" mention renders the channel's real name anyway.
    [GeneratedRegex(@"\[[^\]\n]*\]\(\s*https?://(?:\w+\.)?discord(?:app)?\.com/channels/\d+/(\d+)(?![/\d])\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex MaskedChannelLinkRegex();

    // Any remaining masked link. The url group stops at whitespace or ")", so an unterminated
    // "[text](https://…" — the half-streamed case — doesn't match and is left for a later pass.
    [GeneratedRegex(@"\[([^\]\n]*)\]\(\s*([^)\s]+)\s*\)")]
    private static partial Regex MaskedLinkRegex();

    public static string NormalizeForPlainMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = MaskedChannelLinkRegex().Replace(text, m => $"<#{m.Groups[1].Value}>");
        text = ChannelUrlRegex().Replace(text, m => $"<#{m.Groups[1].Value}>");

        // Keep the model's wording and the clickable URL: "text (url)". A link that only repeats
        // its own target ("[https://x](https://x)") collapses to the bare URL.
        return MaskedLinkRegex().Replace(text, m =>
        {
            var label = m.Groups[1].Value.Trim();
            var url = m.Groups[2].Value;
            return label.Length == 0 || string.Equals(label, url, StringComparison.OrdinalIgnoreCase)
                ? url
                : $"{label} ({url})";
        });
    }
}
