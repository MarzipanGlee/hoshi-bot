namespace HoshiBot.Web.Authorization;

// Names for the second Discord OAuth handler — the one /me uses to prove that another Discord
// account belongs to the same person. Kept together so Program.cs's scheme registration and the
// endpoints that challenge/read it can't drift apart, and so the callback path is stated once
// (it also has to be registered as an OAuth2 redirect URI on the Discord application).
public static class DiscordAccountLink
{
    public const string Scheme = "DiscordAccountLink";

    // Where the proven identity lands instead of the login cookie.
    public const string ExternalScheme = "DiscordAccountLinkExternal";

    public const string CallbackPath = "/signin-discord-link";

    // Where the user comes back to, and the ?link= values /me renders a message for.
    public const string ReturnPath = "/me/link-discord/callback";

    public const string StartPath = "/me/link-discord";

    public const string ResultQueryKey = "link";

    // Claim type carrying the user's Discord client locale — mapped on the *main* login
    // scheme (not the link scheme) from /users/@me's "locale"; consumed by /me's language
    // selector. Only present in sessions signed in after the mapping was added.
    public const string LocaleClaim = "urn:discord:locale";
}
