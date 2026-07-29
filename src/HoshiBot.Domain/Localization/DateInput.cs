using System.Globalization;

namespace HoshiBot.Domain.Localization;

// Modal date/time text-input conventions per language: what the bot prefills into edit
// modals (matching the *.DatePlaceholder/*.TimePlaceholder catalog hints — de shows
// TT.MM.JJJJ, others ISO) and what it accepts back. Parsing is deliberately permissive
// per docs/localization-plan.md "Dates": the resolved language's culture short pattern
// first, then the German dd.MM.yyyy every input used before localization, then ISO — so
// established German typing habits keep working whatever the resolved language, and a
// prefill produced by Format* always parses back via TryParse* for the same language.
public static class DateInput
{
    public const string TimeFormat = "HH:mm";

    public static string DateFormat(Language language) =>
        language == Language.De ? "dd.MM.yyyy" : "yyyy-MM-dd";

    public static string FormatDate(DateTimeOffset value, Language language) =>
        value.ToString(DateFormat(language), CultureInfo.InvariantCulture);

    public static string FormatTime(DateTimeOffset value) =>
        value.ToString(TimeFormat, CultureInfo.InvariantCulture);

    // Exact against the culture's short pattern rather than a free-form culture parse:
    // en-US would happily read "01.08.2026" as January 8, silently flipping day/month
    // for a German-styled date — the exact-pattern chain keeps dotted input German.
    public static bool TryParseDate(string? text, Language language, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var culture = Languages.ToCulture(language);
        var trimmed = text.Trim();
        return DateOnly.TryParseExact(trimmed, culture.DateTimeFormat.ShortDatePattern, culture, DateTimeStyles.None, out date)
            || DateOnly.TryParseExact(trimmed, "dd.MM.yyyy", out date)
            || DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", out date);
    }

    // Free-form culture parse, unlike dates: times carry no day/month ambiguity, and an
    // exact parse against ICU's ShortTimePattern rejects a plain-space "2:30 PM" (the
    // pattern's designator spacing is a narrow no-break space on Linux/ICU).
    public static bool TryParseTime(string? text, Language language, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        return TimeOnly.TryParse(trimmed, Languages.ToCulture(language), DateTimeStyles.None, out time)
            || TimeOnly.TryParseExact(trimmed, TimeFormat, out time);
    }
}
