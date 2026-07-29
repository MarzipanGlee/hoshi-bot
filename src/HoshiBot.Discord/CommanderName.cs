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

    // The "Commander {name}, " salutation prefix. Catalog messages carry the salutation inside
    // their own full-sentence templates (localization sub-phase 6d dissolved the old
    // Greeting/Address concatenations); this survives only for prefixing DYNAMIC text that has
    // no template — AiChat's LLM-composed answers. "Commander" is the in-game address in every
    // supported language, so the prefix itself needs no catalog entry.
    public static string Greeting(User user) => $"Commander {Of(user)}, ";
}
