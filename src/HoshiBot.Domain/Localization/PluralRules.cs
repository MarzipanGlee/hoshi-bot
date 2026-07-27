namespace HoshiBot.Domain.Localization;

// CLDR-lite plural-form selection for the languages in Language — integer cardinals
// only (message counts), which is all the bot ever pluralizes. Forms name the catalog
// key suffixes: "{keyBase}.one" / ".few" / ".many" / ".other".
public static class PluralRules
{
    public static string Select(Language language, int count)
    {
        var n = Math.Abs(count);
        return language switch
        {
            // Russian: 1/21/31… "one"; 2-4/22-24… "few" (except 12-14); rest "many".
            Language.Ru =>
                n % 10 == 1 && n % 100 != 11 ? "one"
                : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "few"
                : "many",

            // Japanese/Korean: no plural distinction.
            Language.Ja or Language.Ko => "other",

            // French/Portuguese treat 0 and 1 as singular (CLDR i = 0..1).
            Language.Fr or Language.Pt => n <= 1 ? "one" : "other",

            // English/German/Spanish/Italian: exactly 1 is singular.
            _ => n == 1 ? "one" : "other",
        };
    }
}
