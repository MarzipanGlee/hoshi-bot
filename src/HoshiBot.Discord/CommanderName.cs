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

    // The shared "Commander {name}, " opening every personal, user-addressed message uses, so it's
    // written the same way everywhere instead of hand-rolled per handler. Greeting returns just the
    // prefix (for concatenating a longer body); Address prepends it to a message. The string
    // overloads are for service-layer code that only has a resolved name, not a User object.
    public static string Greeting(User user) => Greeting(Of(user));

    public static string Greeting(string name) => $"Commander {name}, ";

    public static string Address(User user, string message) => Greeting(user) + message;

    public static string Address(string name, string message) => Greeting(name) + message;
}
