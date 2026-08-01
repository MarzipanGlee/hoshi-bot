using System.Globalization;
using System.Text;

namespace HoshiBot.Domain;

// Rewrites an alliance tag into look-alike ASCII for use as a Discord role name: ĶÅØŞ -> KAOS,
// ВСТК -> BCTK. Deliberately VISUAL, not phonetic — the point of a tag role is that it still reads
// as the tag, so the Cyrillic В becomes the B it looks like, not the "v" it sounds like.
//
// That is why this is not CyrillicTransliterator (phonetic, Cyrillic-only, lowercase output, built
// for matching STFC system names) and not SystemNamePhoneticKey (lossy on purpose — collapses
// doubled letters and folds ph/ck/kh, which would turn the tag ØØ into a single O).
//
// Roughly a third of real alliance tags carry non-ASCII characters, spread over Latin diacritics,
// Cyrillic, Greek, CJK, Kana and Hangul. The first three are handled; the rest have no look-alike
// and pass through untouched, so a tag like 乃IΣ尺 simply keeps its own role.
public static class AllianceTagLatinizer
{
    // Letters that survive NFD decomposition intact — their "diacritic" is part of the glyph, so
    // stripping combining marks does nothing for them and they need naming individually.
    private static readonly Dictionary<char, string> LatinLookalikes = new()
    {
        ['Ø'] = "O",
        ['ø'] = "o",
        ['Æ'] = "AE",
        ['æ'] = "ae",
        ['Œ'] = "OE",
        ['œ'] = "oe",
        ['Đ'] = "D",
        ['đ'] = "d",
        ['Ð'] = "D",
        ['ð'] = "d",
        ['Þ'] = "TH",
        ['þ'] = "th",
        ['Ł'] = "L",
        ['ł'] = "l",
        ['ß'] = "SS",
        ['Ħ'] = "H",
        ['ħ'] = "h",
        ['Ŧ'] = "T",
        ['ŧ'] = "t",
        ['Ɵ'] = "O",
        ['Ə'] = "E",
        ['ə'] = "e",
    };

    // Cyrillic and Greek characters that LOOK like a Latin letter. Only the unambiguous ones —
    // a letter with no Latin twin (Ж, Ю, Щ, ψ, …) is better left alone than mapped to something
    // arbitrary, since the tag stops being recognisable either way.
    private static readonly Dictionary<char, string> ScriptLookalikes = new()
    {
        // Cyrillic
        ['А'] = "A",
        ['В'] = "B",
        ['С'] = "C",
        ['Е'] = "E",
        ['Ё'] = "E",
        ['Н'] = "H",
        ['К'] = "K",
        ['М'] = "M",
        ['О'] = "O",
        ['Р'] = "P",
        ['Т'] = "T",
        ['У'] = "Y",
        ['Х'] = "X",
        ['І'] = "I",
        ['Ї'] = "I",
        ['Ј'] = "J",
        ['Ѕ'] = "S",
        ['З'] = "3",
        ['Ф'] = "F",
        ['Д'] = "D",
        ['И'] = "N",
        ['Я'] = "R",
        ['Г'] = "R",
        ['Л'] = "N",
        ['П'] = "N",
        ['Ч'] = "4",
        ['Б'] = "6",
        ['а'] = "a",
        ['в'] = "b",
        ['с'] = "c",
        ['е'] = "e",
        ['ё'] = "e",
        ['н'] = "h",
        ['к'] = "k",
        ['м'] = "m",
        ['о'] = "o",
        ['р'] = "p",
        ['т'] = "t",
        ['у'] = "y",
        ['х'] = "x",
        ['і'] = "i",
        ['ї'] = "i",
        ['ј'] = "j",
        ['ѕ'] = "s",
        ['з'] = "3",
        ['ф'] = "f",
        ['д'] = "d",
        ['и'] = "n",
        ['я'] = "r",
        ['г'] = "r",
        ['л'] = "n",
        ['п'] = "n",
        ['ч'] = "4",
        ['б'] = "6",

        // Greek
        ['Α'] = "A",
        ['Β'] = "B",
        ['Ε'] = "E",
        ['Ζ'] = "Z",
        ['Η'] = "H",
        ['Ι'] = "I",
        ['Κ'] = "K",
        ['Μ'] = "M",
        ['Ν'] = "N",
        ['Ο'] = "O",
        ['Ρ'] = "P",
        ['Τ'] = "T",
        ['Υ'] = "Y",
        ['Χ'] = "X",
        ['Ω'] = "O",
        ['Ξ'] = "E",
        ['Λ'] = "A",
        ['Σ'] = "S",
        ['Φ'] = "F",
        ['Δ'] = "D",
        ['Θ'] = "O",
        ['Π'] = "N",
        ['α'] = "a",
        ['β'] = "b",
        ['ε'] = "e",
        ['ζ'] = "z",
        ['η'] = "n",
        ['ι'] = "i",
        ['κ'] = "k",
        ['μ'] = "u",
        ['ν'] = "v",
        ['ο'] = "o",
        ['ρ'] = "p",
        ['τ'] = "t",
        ['υ'] = "u",
        ['χ'] = "x",
        ['ω'] = "w",
        ['σ'] = "o",
        ['φ'] = "f",
        ['δ'] = "d",
    };

    // True when latinizing would actually change the tag — lets callers skip building a duplicate
    // candidate role name for a tag that is already plain ASCII.
    public static bool NeedsLatinizing(string tag) => ToLatin(tag) != tag;

    public static string ToLatin(string tag)
    {
        var builder = new StringBuilder(tag.Length);
        foreach (var c in tag)
        {
            if (LatinLookalikes.TryGetValue(c, out var latin) || ScriptLookalikes.TryGetValue(c, out latin))
            {
                builder.Append(latin);
                continue;
            }

            // Everything else goes through NFD and loses its combining marks, which covers the
            // ordinary accented Latin letters (Å Ş Ķ É Ü Ñ Î …) in one rule instead of a table.
            // A character that decomposes to nothing usable (CJK, Kana, Hangul) survives as-is.
            var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
            foreach (var d in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark)
                    builder.Append(d);
            }
        }

        return builder.ToString();
    }
}
