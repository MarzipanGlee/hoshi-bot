using System.Globalization;
using System.Text;

namespace HoshiBot.Domain;

// The searchable form of a player or alliance name: what someone would type on a plain Latin
// keyboard to find it.
//
// STFC names are full of homoglyphs and decoration, so the names hardest to find are exactly the
// ones you cannot type — "Gоrn" holds a Cyrillic о, "Аlеx92" a Cyrillic А and е, "stΕVΕN" a Greek Ε.
// Comparing typed text against the stored name means those rows are unreachable.
//
// The mapping is VISUAL, not phonetic. "Кирвин" becomes "knpbnh" because that is what the shapes
// look like — nobody types "kirvin" while staring at Cyrillic they don't read. That is the opposite
// choice from CyrillicTransliterator (which is phonetic, and right for system names announced in the
// game's Russian client).
//
// It is also more aggressive than AllianceTagLatinizer, which deliberately refuses ambiguous letters
// (и, ж, ю): that one names roles a human reads, where a wrong guess makes the role unrecognisable.
// A key is never displayed, so it only has to agree with what someone types — a slightly odd
// mapping costs nothing as long as it is the same on both sides.
//
// Collisions are fine. Two players landing on one key just means the picker offers both.
public static class PlayerNameKey
{
    // Letters that survive NFD intact, so stripping combining marks does nothing for them.
    private static readonly Dictionary<char, string> LatinLookalikes = new()
    {
        ['Ø'] = "o",
        ['ø'] = "o",
        ['Ǿ'] = "o",
        ['ǿ'] = "o",
        ['Æ'] = "ae",
        ['æ'] = "ae",
        ['Œ'] = "oe",
        ['œ'] = "oe",
        ['Đ'] = "d",
        ['đ'] = "d",
        ['Ð'] = "d",
        ['ð'] = "d",
        ['Þ'] = "th",
        ['þ'] = "th",
        ['Ł'] = "l",
        ['ł'] = "l",
        ['ß'] = "ss",
        ['Ħ'] = "h",
        ['ħ'] = "h",
        ['Ŧ'] = "t",
        ['ŧ'] = "t",
        ['Ɵ'] = "o",
        ['Ə'] = "e",
        ['ə'] = "e",
        ['ı'] = "i",
        ['İ'] = "i",
    };

    // Cyrillic by SHAPE. Upper and lower case are listed separately because the shapes genuinely
    // differ — Д is a triangle where д is not, П is two posts where п is not.
    //
    // The readings below were taken from a native reader typing four real catalog names:
    //   Сергей1970я → cepren1970r   (г r, й n, я r)
    //   ОЛЕГ1984    → oner1984      (Л n, Г r)
    //   ВолкодавСПб → bonkoaabcn6   (л n, д a, П n, б 6)
    //   13тый       → 13tbin        (ы bi, й n)
    private static readonly Dictionary<char, string> Cyrillic = new()
    {
        ['А'] = "a",
        ['а'] = "a",
        ['Б'] = "6",
        ['б'] = "6",
        ['В'] = "b",
        ['в'] = "b",
        ['Г'] = "r",
        ['г'] = "r",
        ['Д'] = "a",
        ['д'] = "a",
        ['Е'] = "e",
        ['е'] = "e",
        ['Ё'] = "e",
        ['ё'] = "e",
        ['Ж'] = "x",
        ['ж'] = "x",
        ['З'] = "3",
        ['з'] = "3",
        ['И'] = "n",
        ['и'] = "n",
        ['Й'] = "n",
        ['й'] = "n",
        ['К'] = "k",
        ['к'] = "k",
        ['Л'] = "n",
        ['л'] = "n",
        ['М'] = "m",
        ['м'] = "m",
        ['Н'] = "h",
        ['н'] = "h",
        ['О'] = "o",
        ['о'] = "o",
        ['П'] = "n",
        ['п'] = "n",
        ['Р'] = "p",
        ['р'] = "p",
        ['С'] = "c",
        ['с'] = "c",
        ['Т'] = "t",
        ['т'] = "t",
        ['У'] = "y",
        ['у'] = "y",
        ['Ф'] = "o",
        ['ф'] = "o",
        ['Х'] = "x",
        ['х'] = "x",
        ['Ц'] = "u",
        ['ц'] = "u",
        ['Ч'] = "4",
        ['ч'] = "4",
        ['Ш'] = "w",
        ['ш'] = "w",
        ['Щ'] = "w",
        ['щ'] = "w",
        ['Ъ'] = "b",
        ['ъ'] = "b",
        ['Ы'] = "bi",
        ['ы'] = "bi",
        ['Ь'] = "b",
        ['ь'] = "b",
        ['Э'] = "e",
        ['э'] = "e",
        ['Ю'] = "io",
        ['ю'] = "io",
        ['Я'] = "r",
        ['я'] = "r",
        // Ukrainian/Belarusian letters that show up in tags and names.
        ['Є'] = "e",
        ['є'] = "e",
        ['І'] = "i",
        ['і'] = "i",
        ['Ї'] = "i",
        ['ї'] = "i",
        ['Ґ'] = "r",
        ['ґ'] = "r",
    };

    // Greek by shape, same principle.
    private static readonly Dictionary<char, string> Greek = new()
    {
        ['Α'] = "a",
        ['α'] = "a",
        ['Β'] = "b",
        ['β'] = "b",
        ['Γ'] = "r",
        ['γ'] = "y",
        ['Δ'] = "a",
        ['δ'] = "d",
        ['Ε'] = "e",
        ['ε'] = "e",
        ['Ζ'] = "z",
        ['ζ'] = "z",
        ['Η'] = "h",
        ['η'] = "n",
        ['Θ'] = "o",
        ['θ'] = "o",
        ['Ι'] = "i",
        ['ι'] = "i",
        ['Κ'] = "k",
        ['κ'] = "k",
        ['Λ'] = "a",
        ['λ'] = "a",
        ['Μ'] = "m",
        ['μ'] = "u",
        ['Ν'] = "n",
        ['ν'] = "v",
        ['Ξ'] = "e",
        ['ξ'] = "e",
        ['Ο'] = "o",
        ['ο'] = "o",
        ['Π'] = "n",
        ['π'] = "n",
        ['Ρ'] = "p",
        ['ρ'] = "p",
        ['Σ'] = "s",
        ['σ'] = "o",
        ['Τ'] = "t",
        ['τ'] = "t",
        ['Υ'] = "y",
        ['υ'] = "u",
        ['Φ'] = "o",
        ['φ'] = "f",
        ['Χ'] = "x",
        ['χ'] = "x",
        ['Ψ'] = "y",
        ['ψ'] = "y",
        ['Ω'] = "o",
        ['ω'] = "w",
    };

    // CJK characters used as LETTERS rather than as ornament: "Cap七ain山usel七om" is CaptainWuseltom
    // and "七he刃ealKipCom" is TheRealKipCom. Dropping these would break the word in the middle and
    // leave nothing typeable.
    //
    // Only the ones actually seen standing in for a letter are here — the far commoner use is
    // wrapping a name (神Ukitø神, Choco猫, Para忠勇真孝), and those keep getting dropped: they frame a
    // name rather than spell it, and a key without them is the one that matches a Discord nickname.
    // The cost of guessing wrong in this direction is small, since a substring search still finds
    // the rest of the name either way.
    private static readonly Dictionary<char, string> CjkLetters = new()
    {
        ['七'] = "t",
        ['山'] = "w",
        ['刃'] = "r",
    };

    // SMALL CAPITAL letters, the one styled alphabet compatibility decomposition can't reach:
    // Unicode classes these as phonetic letters in their own right rather than styled duplicates of
    // A/L/E, so FormKD leaves them alone and ᴀʟᴇx reduced to "x" — the player unfindable, which is
    // the same failure the Fraktur names had.
    //
    // 25 letters, not 26: Unicode has no SMALL CAPITAL X. Nicknames use a plain "x" or the modifier
    // letter ˣ instead, and that one already folds through FormKD as a superscript.
    //
    // Generated from the Unicode names (LATIN LETTER SMALL CAPITAL *) rather than typed by eye —
    // they are scattered across three blocks and several are IPA letters that look nothing like
    // their code point neighbours.
    private static readonly Dictionary<char, string> SmallCapitals = new()
    {
        ['ᴀ'] = "a",
        ['ʙ'] = "b",
        ['ᴄ'] = "c",
        ['ᴅ'] = "d",
        ['ᴇ'] = "e",
        ['ꜰ'] = "f",
        ['ɢ'] = "g",
        ['ʜ'] = "h",
        ['ɪ'] = "i",
        ['ᴊ'] = "j",
        ['ᴋ'] = "k",
        ['ʟ'] = "l",
        ['ᴍ'] = "m",
        ['ɴ'] = "n",
        ['ᴏ'] = "o",
        ['ᴘ'] = "p",
        ['ꞯ'] = "q",
        ['ʀ'] = "r",
        ['ꜱ'] = "s",
        ['ᴛ'] = "t",
        ['ᴜ'] = "u",
        ['ᴠ'] = "v",
        ['ᴡ'] = "w",
        ['ʏ'] = "y",
        ['ᴢ'] = "z",
    };

    // What a search compares against, on both sides. Empty when the name is nothing but decoration —
    // callers treat that as "no key", never as "matches everything".
    public static string Compute(string name)
    {
        var builder = new StringBuilder(name.Length);

        // Runes, not chars. Iterating UTF-16 code units hands String.Normalize half a surrogate
        // pair for anything outside the BMP, which it refuses outright — one emoji or one 𝔊𝔬𝔱𝔥𝔦𝔠
        // letter in a nickname was enough to abort a whole guild's player-link sync. Runes also make
        // an unpaired surrogate harmless: EnumerateRunes yields U+FFFD rather than throwing, and
        // U+FFFD is not a letter, so it drops out below like any other decoration.
        foreach (var rune in name.EnumerateRunes())
        {
            var c = rune.IsBmp ? (char)rune.Value : '\0';
            if (LatinLookalikes.TryGetValue(c, out var mapped)
                || Cyrillic.TryGetValue(c, out mapped)
                || Greek.TryGetValue(c, out mapped)
                || CjkLetters.TryGetValue(c, out mapped)
                || SmallCapitals.TryGetValue(c, out mapped))
            {
                builder.Append(mapped);
                continue;
            }

            // COMPATIBILITY decomposition (FormKD), not canonical (FormD). Canonical only strips
            // accents — é to e. Compatibility also folds the styled Latin that Discord nicknames are
            // full of back to the letters they depict: 𝔊𝔬𝔱𝔥𝔦𝔠 is the word "Gothic" written in
            // Mathematical Fraktur, and reading it as decoration to be dropped left that player with
            // no key at all and therefore no way to be found. Same for ᴀ small caps, Ａ full width,
            // ① circled digits and the ﬁ ligature.
            foreach (var d in rune.ToString().Normalize(NormalizationForm.FormKD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(d) == UnicodeCategory.NonSpacingMark)
                    continue;

                // Everything that is not a letter or digit is decoration: the katakana in ッNobodyッ,
                // the CJK in Para忠勇真孝, spaces, brackets, punctuation. Dropping it is what makes
                // "nobody" find the first, and it folds "Alex 92" and "Alex92" onto one key.
                // The handful of CJK that stand in for a letter are mapped above before this.
                var lower = char.ToLowerInvariant(d);
                if (char.IsAsciiLetterOrDigit(lower))
                    builder.Append(lower);
            }
        }

        return builder.ToString();
    }
}
