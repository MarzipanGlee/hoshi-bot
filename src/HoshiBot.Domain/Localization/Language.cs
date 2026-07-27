using System.Globalization;

namespace HoshiBot.Domain.Localization;

// The languages the bot can speak — the full STFC in-game set (see docs/backlog.md
// "STFC in-game languages — i18n reference"), so storage/selector values never need a
// schema change as locales are enabled. Which of these are actually offered is gated by
// Languages.Enabled.
public enum Language
{
    En,
    De,
    Fr,
    It,
    Es,
    Ru,
    Pt,
    Ja,
    Ko,
}

public static class Languages
{
    // The launch gate: selectors offer these, and the catalog completeness test enforces
    // full key parity for exactly these locales. Enabling another language = add its
    // Locales/xx.json (+ Localizations/xx.json for command descriptions) and append here.
    public static readonly IReadOnlyList<Language> Enabled = [Language.En, Language.De];

    // DB representation (lowercase ISO 639-1) — readable in psql and directly comparable
    // with Discord locale prefixes.
    public static string ToCode(Language language) => language switch
    {
        Language.En => "en",
        Language.De => "de",
        Language.Fr => "fr",
        Language.It => "it",
        Language.Es => "es",
        Language.Ru => "ru",
        Language.Pt => "pt",
        Language.Ja => "ja",
        Language.Ko => "ko",
        _ => "en",
    };

    public static Language? Parse(string? code) => code?.ToLowerInvariant() switch
    {
        "en" => Language.En,
        "de" => Language.De,
        "fr" => Language.Fr,
        "it" => Language.It,
        "es" => Language.Es,
        "ru" => Language.Ru,
        "pt" => Language.Pt,
        "ja" => Language.Ja,
        "ko" => Language.Ko,
        _ => null,
    };

    // Maps a Discord locale ("en-US", "pt-BR", "es-419", "de", …) onto a supported
    // language by its prefix. Unknown/unsupported locales yield null so callers fall
    // through their resolution chain instead of silently landing on English.
    public static Language? FromDiscordLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return null;

        var dash = locale.IndexOf('-');
        return Parse(dash > 0 ? locale[..dash] : locale);
    }

    // For date/number formatting (weekday names etc.). Region choices are pragmatic
    // defaults; Discord's own <t:…> timestamps stay per-viewer and don't go through this.
    public static CultureInfo ToCulture(Language language) => CultureInfo.GetCultureInfo(language switch
    {
        Language.En => "en-US",
        Language.De => "de-DE",
        Language.Fr => "fr-FR",
        Language.It => "it-IT",
        Language.Es => "es-ES",
        Language.Ru => "ru-RU",
        Language.Pt => "pt-BR",
        Language.Ja => "ja-JP",
        Language.Ko => "ko-KR",
        _ => "en-US",
    });

    // For LLM answer-language instructions ("Answer in German.") — prompts are English,
    // so the English exonym is the right form.
    public static string EnglishName(Language language) => language switch
    {
        Language.En => "English",
        Language.De => "German",
        Language.Fr => "French",
        Language.It => "Italian",
        Language.Es => "Spanish",
        Language.Ru => "Russian",
        Language.Pt => "Portuguese",
        Language.Ja => "Japanese",
        Language.Ko => "Korean",
        _ => "English",
    };

    // Selector display name: the language's own name (endonym) so every user recognizes
    // their language regardless of the UI's language.
    public static string NativeName(Language language) => language switch
    {
        Language.En => "English",
        Language.De => "Deutsch",
        Language.Fr => "Français",
        Language.It => "Italiano",
        Language.Es => "Español",
        Language.Ru => "Русский",
        Language.Pt => "Português",
        Language.Ja => "日本語",
        Language.Ko => "한국어",
        _ => "English",
    };
}
