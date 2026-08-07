using System.Text.RegularExpressions;

namespace HoshiBot.Domain;

// Repairs channel mentions in admin-authored text (the Channel Guide message).
//
// Discord only renders a channel as a clickable pill for the exact syntax "<#id>". Nobody types
// that: an admin writing the text in a browser textarea reaches for the same thing they do in
// Discord — type "#" and pick, or copy a pill straight out of the client. Neither produces "<#id>":
//
//   - Copying a pill yields its DISPLAY text — "#", a U+2060 word joiner, then the channel's name,
//     emoji and all ("#⁠💬│tipps-chat"). Pasted back, it is inert text that merely looks right,
//     which is exactly how it reached production: a guide listing six channels, none clickable.
//   - Typing "#tipps-chat" by hand does nothing at all without an autocomplete behind it.
//
// So the editor resolves both forms to "<#id>" on save. The alternative — telling admins to hunt
// for channel ids in Discord's developer mode — is not a real instruction to give somebody.
public static partial class ChannelMentionText
{
    // Word joiner / zero-width space and friends: invisible, and they ride along on a copied pill,
    // where they would otherwise break an exact name match against the channel list.
    [GeneratedRegex(@"[​⁠-⁤﻿]")]
    private static partial Regex InvisibleRegex();

    // A "#" followed by a run of non-space characters. Discord channel names contain no spaces but
    // very much do contain emoji and box-drawing characters, so the run is everything up to the
    // next whitespace rather than a conservative [a-z0-9-]. The optional "@" catches the copied-id
    // form some clients produce. A markdown heading ("# Title") is safe: the space ends the run
    // before it starts.
    [GeneratedRegex(@"<#\d+>|#@?([^\s<>#]+)")]
    private static partial Regex CandidateRegex();

    private const int MinSnowflakeDigits = 17;
    private const int MaxSnowflakeDigits = 20;

    /// <param name="resolveChannelByName">
    /// Channel id for an exact display-name match, or null. Case-insensitivity and any other
    /// leniency belong to the caller, which is the side that knows the guild's channels.
    /// </param>
    public static string Normalize(string? text, Func<string, ulong?> resolveChannelByName)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        var cleaned = InvisibleRegex().Replace(text, "");

        return CandidateRegex().Replace(cleaned, match =>
        {
            // Already a real mention — leave it exactly as it is.
            if (!match.Groups[1].Success)
                return match.Value;

            var candidate = match.Groups[1].Value;

            // A bare id pasted after the "#".
            if (candidate.Length is >= MinSnowflakeDigits and <= MaxSnowflakeDigits
                && candidate.All(char.IsAsciiDigit)
                && ulong.TryParse(candidate, out var pastedId))
                return $"<#{pastedId}>";

            // Anything else only becomes a mention if it actually names a channel in this guild.
            // Unresolvable text is left untouched rather than guessed at — "#1" in prose is not a
            // broken mention, and silently eating it would be worse than leaving it alone.
            return resolveChannelByName(candidate) is { } id ? $"<#{id}>" : match.Value;
        });
    }
}
