using System.Text.RegularExpressions;
using NetCord;

namespace HoshiBot.Discord;

// Ported from legacy's $defs.RegEx.MemberName ("\[.*\]\s*", applied to .Member.Nick in
// nearly every command handler) — strips a leading alliance/clan tag like "[LF] " from a
// guild nickname so "Commander {name}" reads naturally instead of "Commander [LF] Name".
public static partial class CommanderName
{
    [GeneratedRegex(@"\[.*\]\s*")]
    private static partial Regex TagPattern();

    // Context.User is always actually a GuildInteractionUser (has Nickname) for any
    // guild-context interaction — every command/component here is guild-only — but this
    // falls back to the global display name/username defensively rather than assuming so.
    public static string Of(User user)
    {
        var name = (user as GuildUser)?.Nickname ?? user.GlobalName ?? user.Username;
        return TagPattern().Replace(name, "");
    }
}
