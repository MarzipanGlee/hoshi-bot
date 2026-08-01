using HoshiBot.Domain;
using NetCord;

namespace HoshiBot.Discord;

// Strips a guild nickname back to the bare name for salutations — the leading alliance/clan tag
// ("[TAG] ") and the member's own Nickname Sync suffix (" (Suffix)") both come off, so
// "Commander {name}" reads naturally. The strip itself lives in NicknameComposer.Strip next to the
// composition it inverts; this is just the User-shaped entry point.
public static class CommanderName
{
    // Context.User is always actually a GuildInteractionUser (has Nickname) for any
    // guild-context interaction — every command/component here is guild-only — but this
    // falls back to the global display name/username defensively rather than assuming so.
    public static string Of(User user)
    {
        var name = (user as GuildUser)?.Nickname ?? user.GlobalName ?? user.Username;
        return NicknameComposer.Strip(name);
    }

    // The "Commander {name}, " salutation prefix. Catalog messages carry the salutation inside
    // their own full-sentence templates (localization sub-phase 6d dissolved the old
    // Greeting/Address concatenations); this survives only for prefixing DYNAMIC text that has
    // no template — AiChat's LLM-composed answers. "Commander" is the in-game address in every
    // supported language, so the prefix itself needs no catalog entry.
    public static string Greeting(User user) => $"Commander {Of(user)}, ";
}
