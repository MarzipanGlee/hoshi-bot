using System.Text.RegularExpressions;

namespace HoshiBot.Domain;

// Repairs channel and role mentions in admin-authored text (the Channel Guide message).
//
// Discord only renders a clickable pill for the exact "<#id>" / "<@&id>" syntax. Nobody types that:
// an admin writing the text in a browser textarea reaches for the same thing they do in Discord —
// type "#"/"@" and pick, or copy a pill straight out of the client. Neither produces the real form:
//
//   - Copying a pill yields its DISPLAY text — the sigil, a U+2060 word joiner, then the name,
//     emoji and all ("#⁠💬│tipps-chat"). Pasted back, it is inert text that merely looks right,
//     which is exactly how it reached production: a guide listing six channels, none clickable.
//   - Typing "#tipps-chat" by hand does nothing at all without an autocomplete behind it.
//
// So the editor resolves both forms on save. The alternative — telling admins to hunt for ids in
// Discord's developer mode — is not a real instruction to give somebody.
//
// Channels and roles need different matching. A channel name can't contain a space, so its
// candidate is simply the run up to the next one. A role name very much can ("Command Staff"), so
// roles are matched by trying the guild's actual role names longest-first — there is no way to know
// where "@Command Staff is here" ends without knowing the names.
public static class DiscordMentionText
{
    // Word joiner / zero-width space and friends: invisible, and they ride along on a copied pill,
    // where they would otherwise break an exact name match.
    private static readonly Regex InvisibleRegex = new(@"[​‌‍⁠-⁤﻿]", RegexOptions.Compiled);

    private const int MinSnowflakeDigits = 17;
    private const int MaxSnowflakeDigits = 20;

    // Never turned into a mention even when the guild's role list contains them: they are not what
    // an admin means when writing prose, and "everyone" is a role only as an implementation detail.
    private static readonly string[] NeverRoles = ["everyone", "here"];

    /// <param name="channelsByName">Channel display name → id. Build it with an ignore-case comparer.</param>
    /// <param name="rolesByName">Role name → id. Same.</param>
    public static string Normalize(
        string? text,
        IReadOnlyDictionary<string, ulong> channelsByName,
        IReadOnlyDictionary<string, ulong> rolesByName)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? "";

        var cleaned = InvisibleRegex.Replace(text, "");
        return Matcher(rolesByName).Replace(cleaned, match => Replace(match, channelsByName, rolesByName));
    }

    // Built per call because the role alternation depends on the guild's roles. Ordered longest-first
    // so "@Command Staff" wins over a "@Command" role rather than leaving " Staff" stranded.
    private static Regex Matcher(IReadOnlyDictionary<string, ulong> rolesByName)
    {
        var roleNames = rolesByName.Keys
            .Where(name => !string.IsNullOrWhiteSpace(name) && !NeverRoles.Contains(name, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(name => name.Length)
            .Select(Regex.Escape)
            .ToList();

        // Alternative 1 leaves any already-correct mention (channel, role, or user) untouched.
        // Alternative 2 is a channel candidate: everything up to the next space, since channel names
        // carry emoji and box-drawing characters but never spaces. The optional "@" catches the
        // copied-id form some clients produce. A markdown heading ("# Title") is safe — the space
        // ends the run before it starts.
        var pattern = @"<[@#][&!]?\d+>|\#@?(?<channel>[^\s<>#]+)";
        if (roleNames.Count > 0)
            pattern += $@"|@(?<role>{string.Join("|", roleNames)})";

        return new Regex(pattern, RegexOptions.IgnoreCase);
    }

    private static string Replace(Match match, IReadOnlyDictionary<string, ulong> channelsByName, IReadOnlyDictionary<string, ulong> rolesByName)
    {
        if (match.Groups["role"] is { Success: true } role)
            return rolesByName.TryGetValue(role.Value, out var roleId) ? $"<@&{roleId}>" : match.Value;

        if (match.Groups["channel"] is not { Success: true } channel)
            return match.Value; // Already a real mention.

        // A bare id pasted after the "#".
        if (channel.Value.Length is >= MinSnowflakeDigits and <= MaxSnowflakeDigits
            && channel.Value.All(char.IsAsciiDigit)
            && ulong.TryParse(channel.Value, out var pastedId))
            return $"<#{pastedId}>";

        // Anything else only becomes a mention if it actually names a channel in this guild.
        // Unresolvable text is left untouched rather than guessed at — "#1" in prose is not a broken
        // mention, and silently eating it would be worse than leaving it alone.
        return channelsByName.TryGetValue(channel.Value, out var id) ? $"<#{id}>" : match.Value;
    }
}
