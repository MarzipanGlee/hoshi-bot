using System.Text;

namespace HoshiBot.Domain;

// Reduces a system name (or user-typed input) to a coarse phonetic key so spelling
// variants that sound the same compare equal — most importantly a Cyrillic
// transliteration vs. the real English spelling, e.g. "Донифон" (-> "donifon" via
// CyrillicTransliterator) vs. "Doniphon" (-> "donifon" after the ph->f fold below).
// Best-effort: covers the patterns observed so far, not an exhaustive phonetic algorithm —
// used only as a fallback after an exact/case-insensitive match has already failed.
public static class SystemNamePhoneticKey
{
    public static string Compute(string name)
    {
        var latin = CyrillicTransliterator.ContainsCyrillic(name)
            ? CyrillicTransliterator.ToLatin(name)
            : name;

        return Normalize(latin);
    }

    private static string Normalize(string text)
    {
        var lower = text.ToLowerInvariant()
            .Replace("ph", "f")
            .Replace("ck", "k")
            .Replace("kh", "h");

        var builder = new StringBuilder(lower.Length);
        char? last = null;
        foreach (var c in lower)
        {
            if (!char.IsLetterOrDigit(c))
            {
                last = null; // word boundary — don't collapse across it
                continue;
            }

            if (c == last) // e.g. "tt" -> "t"
                continue;

            builder.Append(c);
            last = c;
        }

        return builder.ToString();
    }
}
