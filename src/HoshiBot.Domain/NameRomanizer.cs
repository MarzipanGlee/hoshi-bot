using System.Text;

namespace HoshiBot.Domain;

// Romanises Japanese kana and Korean Hangul so a name written entirely in them still has something
// a Latin keyboard can type. 大和 stays unreachable — see the Han note at the bottom — but
// やわらか becomes "yawaraka" and 대한민국 becomes "daehanminguk".
//
// Used ONLY as PlayerNameKey's fallback, never as its main path, and that restriction is the whole
// design. PlayerNameKey deliberately drops kana that FRAMES a Latin name — ッNobodyッ keys to
// "nobody", which is what lets a member type "nobody" and find them. Romanising unconditionally
// would turn that into "tsunobodytsu" and break every name that works today. Applying it only when
// nothing typeable survived means it can add reach without taking any away.
public static class NameRomanizer
{
    // Kana are romanised by mora, longest match first, so the two-character digraphs (きゃ = "kya")
    // win over their parts ("ki" + "ya"). Hepburn, because it is what a player typing their own name
    // on a Latin keyboard would produce — し is "shi", not Kunrei's "si".
    private static readonly Dictionary<string, string> Kana = BuildKana();

    // Hangul needs no table: a precomposed syllable decomposes arithmetically into
    // (initial, medial, final), and these are the Revised Romanization letters for each slot.
    private static readonly string[] HangulInitials =
        ["g", "kk", "n", "d", "tt", "r", "m", "b", "pp", "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h"];

    private static readonly string[] HangulMedials =
        ["a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", "eu", "ui", "i"];

    private static readonly string[] HangulFinals =
        ["", "k", "k", "ks", "n", "nj", "nh", "d", "l", "lg", "lm", "lb", "ls", "lt", "lp", "lh", "m", "b", "bs", "s", "ss", "ng", "j", "c", "k", "t", "p", "h"];

    private const int HangulBase = 0xAC00;
    private const int HangulLast = 0xD7A3;
    private const int MedialCount = 21;
    private const int FinalCount = 28;

    public static string Romanize(string name)
    {
        var builder = new StringBuilder(name.Length * 2);

        for (var i = 0; i < name.Length; i++)
        {
            if (TryHangul(name[i], builder))
                continue;

            // Katakana folds onto hiragana first (the blocks are the same order, 0x60 apart), so one
            // table serves both — ツ and つ are the same mora written twice.
            var mora = ToHiragana(name[i]);
            var pair = i + 1 < name.Length ? mora + ToHiragana(name[i + 1]) : null;

            if (pair is not null && Kana.TryGetValue(pair, out var digraph))
            {
                builder.Append(digraph);
                i++;
            }
            else if (Kana.TryGetValue(mora, out var single))
            {
                builder.Append(single);
            }
            else if (mora == "っ")
            {
                // Sokuon: doubles the consonant that follows. Peeking is enough — a trailing っ has
                // nothing to double and is simply dropped.
                var next = i + 1 < name.Length ? ToHiragana(name[i + 1]) : "";
                if (Kana.TryGetValue(next, out var following) && following.Length > 0 && !"aiueo".Contains(following[0]))
                    builder.Append(following[0]);
            }

            // Anything else — the ー prolonged-sound mark, Han characters, punctuation — contributes
            // nothing. A search key gains nothing from vowel length.
        }

        return builder.ToString();
    }

    private static bool TryHangul(char c, StringBuilder builder)
    {
        if (c is < (char)HangulBase or > (char)HangulLast)
            return false;

        var index = c - HangulBase;
        builder.Append(HangulInitials[index / (MedialCount * FinalCount)]);
        builder.Append(HangulMedials[index / FinalCount % MedialCount]);
        builder.Append(HangulFinals[index % FinalCount]);
        return true;
    }

    // Katakana U+30A1-U+30F6 sit exactly 0x60 above their hiragana counterparts.
    private static string ToHiragana(char c) =>
        (c is >= (char)0x30A1 and <= (char)0x30F6 ? (char)(c - 0x60) : c).ToString();

    private static Dictionary<string, string> BuildKana()
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string kana, string romaji)
        {
            var runes = kana.Split(' ');
            var sounds = romaji.Split(' ');
            for (var i = 0; i < runes.Length; i++)
                table[runes[i]] = sounds[i];
        }

        Add("あ い う え お", "a i u e o");
        Add("か き く け こ", "ka ki ku ke ko");
        Add("が ぎ ぐ げ ご", "ga gi gu ge go");
        Add("さ し す せ そ", "sa shi su se so");
        Add("ざ じ ず ぜ ぞ", "za ji zu ze zo");
        Add("た ち つ て と", "ta chi tsu te to");
        Add("だ ぢ づ で ど", "da ji zu de do");
        Add("な に ぬ ね の", "na ni nu ne no");
        Add("は ひ ふ へ ほ", "ha hi fu he ho");
        Add("ば び ぶ べ ぼ", "ba bi bu be bo");
        Add("ぱ ぴ ぷ ぺ ぽ", "pa pi pu pe po");
        Add("ま み む め も", "ma mi mu me mo");
        Add("や ゆ よ", "ya yu yo");
        Add("ら り る れ ろ", "ra ri ru re ro");
        Add("わ ゐ ゑ を ん", "wa wi we wo n");

        // Small kana standing alone (ァ in a stylised name) still carry their vowel.
        Add("ぁ ぃ ぅ ぇ ぉ ゃ ゅ ょ", "a i u e o ya yu yo");

        // Digraphs: an i-row mora plus a small y-kana is one sound, so they must be matched before
        // their parts or きゃ reads "kiya".
        foreach (var (lead, stem) in new[]
        {
            ("き", "k"), ("ぎ", "g"), ("に", "n"), ("ひ", "h"), ("び", "b"), ("ぴ", "p"), ("み", "m"), ("り", "r"),
        })
        {
            table[lead + "ゃ"] = stem + "ya";
            table[lead + "ゅ"] = stem + "yu";
            table[lead + "ょ"] = stem + "yo";
        }

        // The sibilants are irregular in Hepburn: しゃ is "sha", not "shya".
        foreach (var (lead, stem) in new[] { ("し", "sh"), ("ち", "ch"), ("じ", "j"), ("ぢ", "j") })
        {
            table[lead + "ゃ"] = stem + "a";
            table[lead + "ゅ"] = stem + "u";
            table[lead + "ょ"] = stem + "o";
        }

        return table;
    }
}

// Han characters (大和, 連盟, 銀河) are deliberately NOT romanised. A reading needs a language:
// 大和 is "yamato" in Japanese, "dàhé" in Mandarin and "daehwa" in Korean, and the catalogue records
// no language for an alliance. Guessing one would attach a confident wrong name to a real alliance,
// which is worse than leaving it unfindable by name — it can still be reached by id and by server.
