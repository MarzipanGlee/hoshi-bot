using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

public class LanguagePolicyTests
{
    [Theory]
    // explicit wins over everything
    [InlineData("de", "en-US", Language.De)]
    // no explicit: Discord preferred_locale decides
    [InlineData(null, "de", Language.De)]
    [InlineData(null, "en-US", Language.En)]
    // unsupported/unset preferred_locale: terminal English fallback
    [InlineData(null, "pl", Language.En)]
    [InlineData(null, null, Language.En)]
    // stale/invalid explicit value falls through the chain
    [InlineData("xx", "de", Language.De)]
    public void ForGuild_chain(string? explicitCode, string? preferredLocale, Language expected) =>
        Assert.Equal(expected, LanguagePolicy.ForGuild(explicitCode, preferredLocale));

    [Theory]
    [InlineData("en", Language.De, Language.En)]
    [InlineData(null, Language.De, Language.De)]
    [InlineData("xx", Language.De, Language.De)]
    public void ForAlliance_and_ForAudience_inherit_guild(string? code, Language guild, Language expected)
    {
        Assert.Equal(expected, LanguagePolicy.ForAlliance(code, guild));
        Assert.Equal(expected, LanguagePolicy.ForAudience(code, guild));
    }

    [Theory]
    // explicit user preference wins
    [InlineData("de", "en-US", "en-US", Language.En, Language.De)]
    // live interaction locale beats the stored one
    [InlineData(null, "de", "en-US", Language.En, Language.De)]
    // stored locale covers DM/job paths without an interaction
    [InlineData(null, null, "de", Language.En, Language.De)]
    // nothing known about the user: scope fallback
    [InlineData(null, null, null, Language.De, Language.De)]
    // unsupported locales fall through
    [InlineData(null, "pl", "zh-CN", Language.De, Language.De)]
    public void ForUser_chain(string? explicitCode, string? interactionLocale, string? storedLocale, Language scope, Language expected) =>
        Assert.Equal(expected, LanguagePolicy.ForUser(explicitCode, interactionLocale, storedLocale, scope));
}
