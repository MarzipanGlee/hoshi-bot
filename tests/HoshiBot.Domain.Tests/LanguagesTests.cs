using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

public class LanguagesTests
{
    [Theory]
    [InlineData("en", Language.En)]
    [InlineData("en-US", Language.En)]
    [InlineData("en-GB", Language.En)]
    [InlineData("de", Language.De)]
    [InlineData("pt-BR", Language.Pt)]
    [InlineData("es-ES", Language.Es)]
    [InlineData("es-419", Language.Es)]
    [InlineData("fr", Language.Fr)]
    [InlineData("ja", Language.Ja)]
    [InlineData("ko", Language.Ko)]
    [InlineData("ru", Language.Ru)]
    [InlineData("it", Language.It)]
    public void FromDiscordLocale_maps_supported_locales(string locale, Language expected) =>
        Assert.Equal(expected, Languages.FromDiscordLocale(locale));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("zh-CN")]
    [InlineData("pl")]
    [InlineData("tr")]
    public void FromDiscordLocale_returns_null_for_unsupported(string? locale) =>
        Assert.Null(Languages.FromDiscordLocale(locale));

    [Fact]
    public void Code_roundtrips_for_every_language()
    {
        foreach (var language in Enum.GetValues<Language>())
            Assert.Equal(language, Languages.Parse(Languages.ToCode(language)));
    }

    [Fact]
    public void Parse_is_case_insensitive_and_null_safe()
    {
        Assert.Equal(Language.De, Languages.Parse("DE"));
        Assert.Null(Languages.Parse(null));
        Assert.Null(Languages.Parse("xx"));
    }

    [Fact]
    public void Culture_and_names_exist_for_every_language()
    {
        foreach (var language in Enum.GetValues<Language>())
        {
            Assert.NotNull(Languages.ToCulture(language));
            Assert.False(string.IsNullOrWhiteSpace(Languages.EnglishName(language)));
            Assert.False(string.IsNullOrWhiteSpace(Languages.NativeName(language)));
        }
    }

    [Fact]
    public void Enabled_contains_launch_languages() =>
        Assert.Equal([Language.En, Language.De], Languages.Enabled);
}
