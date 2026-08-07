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

    // Found live: PlayerLinkSyncJob died on a member whose nickname carried an astral character.
    // Compute walks UTF-16 code units, so anything outside the BMP arrives as half a surrogate pair,
    // and String.Normalize throws ArgumentException on a lone surrogate. One such nickname aborted
    // the whole guild's sync — every member after it, on every run, indefinitely.
    [Theory]
    [InlineData("Kip🚀Com", "kipcom")]                 // emoji
    [InlineData("𝔊𝔬𝔱𝔥𝔦𝔠", "")]                        // astral "fancy" letters: all decoration
    [InlineData("Alex\ud83d", "alex")]                 // an unpaired surrogate on its own
    [InlineData("\udca9Bob\ud83d", "bob")]             // unpaired on both ends
    public void Astral_characters_do_not_throw(string name, string expected)
    {
        Assert.Equal(expected, PlayerNameKey.Compute(name));
    }
}
