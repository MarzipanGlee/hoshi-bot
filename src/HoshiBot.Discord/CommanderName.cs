using HoshiBot.Domain;
using NetCord;
using NetCord.Gateway;

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

    // Recovers the nickname for a user Discord handed us WITHOUT member data.
    //
    // Discord attaches the member object — and therefore the nickname — to a message delivered over
    // the GATEWAY, but not to one fetched over REST. So the same person reads as "MarzipanGlee"
    // when seen live and "Oops" when the same message is fetched back, which is how a published
    // announcement ended up greeting its own author by their global name.
    //
    // The gateway already caches every member, so this costs nothing: no REST call, which is what
    // makes it usable on the paths that walk hundreds of historical messages. Falls back to the
    // bare user when the guild or member isn't cached.
    public static string Of(GatewayClient client, ulong guildId, User user) =>
        Of(client.Cache.Guilds.TryGetValue(guildId, out var guild) && guild.Users.TryGetValue(user.Id, out var member)
            ? member
            : user);

    // The "Commander {name}, " salutation prefix. Catalog messages carry the salutation inside
    // their own full-sentence templates (localization sub-phase 6d dissolved the old
    // Greeting/Address concatenations); this survives only for prefixing DYNAMIC text that has
    // no template — AiChat's LLM-composed answers. "Commander" is the in-game address in every
    // supported language, so the prefix itself needs no catalog entry.
    public static string Greeting(User user) => $"Commander {Of(user)}, ";
}
