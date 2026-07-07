using HoshiBot.Domain;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class SystemNamePhoneticKeyTests
{
    [Fact]
    public void Compute_CyrillicTransliteration_MatchesRealEnglishSpelling()
    {
        // Confirmed real example: STFC's Russian client shows the system "Doniphon" as
        // "Донифон" — a phonetic transliteration, not a separate localized name.
        var cyrillicKey = SystemNamePhoneticKey.Compute("Донифон");
        var englishKey = SystemNamePhoneticKey.Compute("Doniphon");

        Assert.Equal(englishKey, cyrillicKey);
        Assert.Equal("donifon", englishKey);
    }

    [Fact]
    public void Compute_CyrillicWithHyphenAndPhFold_MatchesRealEnglishSpelling()
    {
        // Confirmed real example: the zone system "Zhian Alpha" (space, "ph") appears in
        // the Russian client as "Жиань-Альфа" (hyphen, "ф"->"f") — exercises both the
        // punctuation-normalization and the ph->f fold together, not just one at a time.
        var cyrillicKey = SystemNamePhoneticKey.Compute("Жиань-Альфа");
        var englishKey = SystemNamePhoneticKey.Compute("Zhian Alpha");

        Assert.Equal(englishKey, cyrillicKey);
        Assert.Equal("zhianalfa", englishKey);
    }

    [Theory]
    [InlineData("Толаин", "Tolain")] // letter-for-letter, no ph/ck/kh fold needed
    [InlineData("Garyb", "Garyb")] // already Latin — sanity check, not a real transliteration case
    [InlineData("Callinon", "Callinon")]
    [InlineData("Тефкари-Альфа", "Tefkari Alpha")] // same zone-suffix pattern as Zhian Alpha
    [InlineData("Тефкари-Бета", "Tefkari Beta")] // "Beta"/"Бета" needs no fold at all
    [InlineData("Сотави", "Sotavi")] // first confirmed example exercising в->v
    [InlineData("Африталис", "Afritalis")] // letter-for-letter, no fold needed
    public void Compute_AdditionalConfirmedExamples_MatchRealSystemName(string input, string realName)
    {
        Assert.Equal(SystemNamePhoneticKey.Compute(realName), SystemNamePhoneticKey.Compute(input));
    }

    [Theory]
    [InlineData("Wolf 359", "wolf359")]
    [InlineData("WOLF 359", "wolf359")]
    [InlineData("Bat'Leth", "batleth")]
    public void Compute_LatinInput_NormalizesCaseAndPunctuation(string input, string expected)
    {
        Assert.Equal(expected, SystemNamePhoneticKey.Compute(input));
    }

    [Fact]
    public void Compute_DoubledConsonants_Collapse()
    {
        Assert.Equal(SystemNamePhoneticKey.Compute("Sattelite"), SystemNamePhoneticKey.Compute("Satelite"));
    }

    [Fact]
    public void Compute_DifferentNames_DoNotCollide()
    {
        Assert.NotEqual(SystemNamePhoneticKey.Compute("Wolf 359"), SystemNamePhoneticKey.Compute("Doniphon"));
    }
}
