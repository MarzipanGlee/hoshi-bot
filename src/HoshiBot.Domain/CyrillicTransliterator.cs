using System.Text;

namespace HoshiBot.Domain;

// Converts Cyrillic text to a Latin phonetic approximation, using the common "practical
// transcription" scheme Russian localizations use for foreign proper nouns (not a formal
// GOST/ISO standard). Confirmed against a real example: STFC's Russian client shows the
// system "Doniphon" as "Донифон" — i.e. ф maps to "f", not the "ph" digraph. Because
// several English spellings can sound identical (ph/f, ck/k, doubled consonants), this
// alone doesn't reconstruct the original spelling exactly — pair with SystemNamePhoneticKey
// on both sides of a comparison instead of comparing raw transliteration output directly.
public static class CyrillicTransliterator
{
    private static readonly Dictionary<char, string> Map = new()
    {
        ['а'] = "a",
        ['б'] = "b",
        ['в'] = "v",
        ['г'] = "g",
        ['д'] = "d",
        ['е'] = "e",
        ['ё'] = "e",
        ['ж'] = "zh",
        ['з'] = "z",
        ['и'] = "i",
        ['й'] = "i",
        ['к'] = "k",
        ['л'] = "l",
        ['м'] = "m",
        ['н'] = "n",
        ['о'] = "o",
        ['п'] = "p",
        ['р'] = "r",
        ['с'] = "s",
        ['т'] = "t",
        ['у'] = "u",
        ['ф'] = "f",
        ['х'] = "kh",
        ['ц'] = "ts",
        ['ч'] = "ch",
        ['ш'] = "sh",
        ['щ'] = "shch",
        ['ъ'] = "",
        ['ы'] = "y",
        ['ь'] = "",
        ['э'] = "e",
        ['ю'] = "yu",
        ['я'] = "ya",
    };

    public static bool ContainsCyrillic(string text) =>
        text.Any(c => c is >= 'Ѐ' and <= 'ӿ');

    public static string ToLatin(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (Map.TryGetValue(char.ToLowerInvariant(c), out var latin))
                builder.Append(latin);
            else
                builder.Append(c);
        }

        return builder.ToString();
    }
}
