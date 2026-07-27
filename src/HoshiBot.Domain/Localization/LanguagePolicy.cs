namespace HoshiBot.Domain.Localization;

// The pure resolution chains behind every language decision (see
// docs/localization-plan.md). Data's LanguageResolver supplies the stored values; these
// functions define precedence — kept pure so the full matrices are unit-testable
// without a database.
public static class LanguagePolicy
{
    // Guild: explicit admin choice, else the Discord guild's preferred_locale, else
    // English — the terminal fallback of every chain.
    public static Language ForGuild(string? explicitCode, string? preferredLocale) =>
        Languages.Parse(explicitCode)
        ?? Languages.FromDiscordLocale(preferredLocale)
        ?? Language.En;

    public static Language ForAlliance(string? allianceCode, Language guildLanguage) =>
        Languages.Parse(allianceCode) ?? guildLanguage;

    public static Language ForAudience(string? audienceCode, Language guildLanguage) =>
        Languages.Parse(audienceCode) ?? guildLanguage;

    // User: explicit preference, else the live interaction locale, else the last-seen
    // stored locale (DMs/jobs without an interaction), else the surrounding scope's
    // language.
    public static Language ForUser(string? explicitCode, string? interactionLocale, string? storedLocale, Language scopeFallback) =>
        Languages.Parse(explicitCode)
        ?? Languages.FromDiscordLocale(interactionLocale)
        ?? Languages.FromDiscordLocale(storedLocale)
        ?? scopeFallback;
}
