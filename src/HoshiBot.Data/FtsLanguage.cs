namespace HoshiBot.Data;

// Maps a guild to the Postgres full-text-search config (a regconfig name) used to index/search its
// AI-chat knowledge content. Shared by the Web editor (dropdown + default) and the Discord-side
// search (query-time to_tsvector). All names here ship with a standard Postgres install.
//
// "simple" (no language-specific stemming) is the safe fallback for any locale we don't map — it
// still does case-folding and tokenizing, just no stem matching.
public static class FtsLanguage
{
    public const string Default = "simple";

    // Discord preferred-locale prefix (the part before any "-REGION") → Postgres config.
    private static readonly Dictionary<string, string> ByLocalePrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["da"] = "danish",
        ["de"] = "german",
        ["en"] = "english",
        ["es"] = "spanish",
        ["fr"] = "french",
        ["it"] = "italian",
        ["hu"] = "hungarian",
        ["nl"] = "dutch",
        ["no"] = "norwegian",
        ["pt"] = "portuguese",
        ["ro"] = "romanian",
        ["fi"] = "finnish",
        ["sv"] = "swedish",
        ["tr"] = "turkish",
        ["ru"] = "russian",
        ["el"] = "greek",
        ["lt"] = "lithuanian",
        ["id"] = "indonesian",
    };

    // The set offered in the editor dropdown AND the whitelist every config value is validated
    // against before it reaches to_tsvector. "simple" first (the default / safe choice).
    public static readonly IReadOnlyList<string> SupportedConfigs =
        new[] { Default }.Concat(ByLocalePrefix.Values.Distinct().OrderBy(v => v)).ToList();

    // The default config for a guild that hasn't picked one — derived from its Discord locale.
    public static string FromDiscordLocale(string? preferredLocale)
    {
        if (string.IsNullOrWhiteSpace(preferredLocale))
            return Default;
        var prefix = preferredLocale.Split('-')[0];
        return ByLocalePrefix.GetValueOrDefault(prefix, Default);
    }

    // Guards the regconfig argument: only a whitelisted name is ever interpolated/parameterized
    // into to_tsvector — anything else falls back to the safe default.
    public static string Normalize(string? config) =>
        !string.IsNullOrWhiteSpace(config) && SupportedConfigs.Contains(config, StringComparer.OrdinalIgnoreCase)
            ? config.ToLowerInvariant()
            : Default;
}
