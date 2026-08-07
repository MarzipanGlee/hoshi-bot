using HoshiBot.Domain;
using Xunit;

namespace HoshiBot.Domain.Tests;

// Every name here is a real row from the testing catalog, not an invented example — the point of the
// feature is that these specific names are unfindable today.
public class PlayerNameKeyTests
{
    // Read out loud by a native reader looking at the shapes, which is what pinned the Cyrillic map
    // down. These four carry г й я Л Г л д П б ы between them.
    [Theory]
    [InlineData("Сергей1970я", "cepren1970r")]
    [InlineData("ОЛЕГ1984", "oner1984")]
    [InlineData("ВолкодавСПб", "bonkoaabcn6")]
    [InlineData("13тый", "13tbin")]
    public void Compute_MatchesWhatAReaderTypes(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Compute(name));

    // A homoglyph or two hidden inside an otherwise Latin name — the case that makes a name look
    // perfectly typeable and then not match.
    [Theory]
    [InlineData("Gоrn", "gorn")]           // Cyrillic о
    [InlineData("Аlеx92", "alex92")]       // Cyrillic А and е
    [InlineData("MrBоо", "mrboo")]         // Cyrillic о twice
    [InlineData("Krеzash", "krezash")]     // Cyrillic е
    [InlineData("JinхJ", "jinxj")]         // Cyrillic х — x by shape, not "kh" by sound
    [InlineData("stΕVΕN", "steven")]       // Greek Ε
    [InlineData("AlphaΩmega", "alphaomega")]
    [InlineData("ZınnAlex", "zinnalex")]   // dotless ı
    public void Compute_FoldsHomoglyphsInLatinNames(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Compute(name));

    [Theory]
    [InlineData("Kaszáló", "kaszalo")]
    [InlineData("Mücke2020", "mucke2020")]
    [InlineData("Schöny79", "schony79")]
    [InlineData("SpockHølliday", "spockholliday")]
    [InlineData("ShádowBædgër", "shadowbaedger")]
    [InlineData("RagnarökSpeed", "ragnarokspeed")]
    [InlineData("DúbravskýPerník87", "dubravskypernik87")]
    [InlineData("JōdaiKarōRogers", "jodaikarorogers")]
    [InlineData("1èreclasse666", "1ereclasse666")]
    public void Compute_StripsDiacritics(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Compute(name));

    // Decoration is dropped, which is the only way "nobody" finds ッNobodyッ.
    [Theory]
    [InlineData("ッNobodyッ", "nobody")]
    [InlineData("Para忠勇真孝", "para")]
    [InlineData("Luc发", "luc")]
    [InlineData("メSCHИUPFЄИメ", "schnupfen")]
    [InlineData("メIиFєяиoメ", "inferno")]
    [InlineData("神Ukitø神", "ukito")]
    [InlineData("光TerrifiedTaser", "terrifiedtaser")]
    [InlineData("Choco猫", "choco")]
    [InlineData("KaputtNix是", "kaputtnix")]
    [InlineData("Lodderich白", "lodderich")]
    public void Compute_DropsDecoration(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Compute(name));

    // CJK standing in for a letter, which decoration-dropping would break in the middle.
    [Theory]
    [InlineData("Cap七ain山usel七om", "captainwuseltom")]
    [InlineData("七he刃ealKipCom", "therealkipcom")]
    public void Compute_MapsCjkUsedAsLetters(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Compute(name));

    [Theory]
    [InlineData("Иван1", "nbah1")]
    [InlineData("Кирвин", "knpbnh")]
    [InlineData("Асура", "acypa")]
    [InlineData("тигренок", "tnrpehok")]
    public void Compute_ReadsCyrillicByShape(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Compute(name));

    // Plain ASCII is only lowercased and stripped of separators — nothing exotic should happen to
    // the 94% of names that are already typeable.
    [Theory]
    [InlineData("Speed", "speed")]
    [InlineData("EvilXP", "evilxp")]
    [InlineData("Alex 92", "alex92")]
    [InlineData("F1N3G31ST", "f1n3g31st")]
    [InlineData("[XF] AlexGrille", "xfalexgrille")]
    public void Compute_LeavesPlainNamesAlone(string name, string expected) =>
        Assert.Equal(expected, PlayerNameKey.Compute(name));

    // The key is what gets stored AND what a typed query is run through, so computing it twice must
    // not drift — otherwise a stored key could stop matching its own name.
    [Theory]
    [MemberData(nameof(EveryRealName))]
    public void Compute_IsIdempotent(string name)
    {
        var once = PlayerNameKey.Compute(name);
        Assert.Equal(once, PlayerNameKey.Compute(once));
    }

    [Theory]
    [MemberData(nameof(EveryRealName))]
    public void Compute_ProducesAKeyForEveryRealName(string name) =>
        Assert.NotEqual("", PlayerNameKey.Compute(name));

    // Real rows with nothing a Latin keyboard could ever type. There is no key to give them, and
    // that is the point of the empty return: "" must be treated as "no key" by every caller, because
    // Contains("") matches every row and == "" would match all of these as one another.
    [Theory]
    [InlineData("キャプテンネモ")]
    [InlineData("ふィミレミ")]
    [InlineData("莫託皇帝")]
    public void Compute_IsEmptyForNamesWithNothingTypeable(string name) =>
        Assert.Equal("", PlayerNameKey.Compute(name));

    public static TheoryData<string> EveryRealName() =>
    [
        "Сергей1970я", "ОЛЕГ1984", "ВолкодавСПб", "13тый",
        "Gоrn", "Аlеx92", "MrBоо", "Krеzash", "JinхJ", "stΕVΕN", "AlphaΩmega", "ZınnAlex",
        "Kaszáló", "Mücke2020", "Schöny79", "SpockHølliday", "ShádowBædgër", "RagnarökSpeed",
        "DúbravskýPerník87", "JōdaiKarōRogers", "1èreclasse666", "DeSchietbüddel", "Ünnep", "Süle",
        "ッNobodyッ", "Para忠勇真孝", "Luc发", "メSCHИUPFЄИメ", "メIиFєяиoメ", "七he刃ealKipCom",
        "Cap七ain山usel七om", "神Ukitø神", "光TerrifiedTaser", "Choco猫", "KaputtNix是", "Lodderich白",
        "山AlexSpeedly", "本ZirpendeGrille",
        "Иван1", "Кирвин", "Асура", "тигренок", "Светослав", "солдат42ру", "RакетаСССР",
        "DiMы4", "ManagerМeđanac", "TåGūSå", "FeòilTiugh", "KapitänNiemand", "TheNotoriuseBèé",
        "ㅌFFㅌX", "Speed", "EvilXP", "F1N3G31ST",
    ];

    // Found live: PlayerLinkSyncJob died on a member whose nickname carried an astral character,
    // aborting the whole guild's sync — every member after it, on every run. Compute walked UTF-16
    // code units, so anything outside the BMP reached String.Normalize as half a surrogate pair,
    // which it refuses. It reads runes now, and an unpaired surrogate becomes U+FFFD instead of an
    // exception.
    [Theory]
    [InlineData("Kip\U0001F680Com", "kipcom")]          // emoji: decoration, dropped
    [InlineData("Alex\ud83d", "alex")]                  // unpaired surrogate on its own
    [InlineData("\udca9Bob\ud83d", "bob")]              // unpaired at both ends
    public void Astral_decoration_is_dropped_without_throwing(string name, string expected)
    {
        Assert.Equal(expected, PlayerNameKey.Compute(name));
    }

    // Styled Latin is the NAME, not decoration around it. Dropping it left the player with an empty
    // key and therefore no way to be found at all — the exact opposite of what the key is for.
    // Compatibility decomposition (FormKD) reads these as the letters they depict; canonical (FormD)
    // only strips accents and leaves them untouched.
    [Theory]
    [InlineData("\U0001D50A\U0001D52C\U0001D531\U0001D525\U0001D526\U0001D520", "gothic")]  // Mathematical Fraktur
    [InlineData("\U0001D5D4\U0001D5DF\U0001D5D8\U0001D5EB", "alex")]                          // Mathematical Sans-Serif Bold
    [InlineData("\uFF21\uFF4C\uFF45\uFF58", "alex")]                                          // full width
    [InlineData("Alex\u2460", "alex1")]                                                          // circled digit
    public void Styled_latin_folds_back_to_the_letters_it_depicts(string name, string expected)
    {
        Assert.Equal(expected, PlayerNameKey.Compute(name));
    }

    // Not everything decorative is reachable this way, and it's worth pinning what isn't. Unicode
    // gives SMALL CAPITAL letters no compatibility decomposition — it classes them as phonetic
    // letters in their own right rather than styled duplicates of A/L/E — so ᴀʟᴇx still reduces to
    // "x". Closing that needs entries in LatinLookalikes, the same way the Cyrillic and Greek
    // lookalikes are handled; this test exists so the gap is a known one rather than a surprise.
    [Fact]
    public void Small_capitals_are_a_known_gap()
    {
        Assert.Equal("x", PlayerNameKey.Compute("\u1D00\u029F\u1D07x"));
    }

}
