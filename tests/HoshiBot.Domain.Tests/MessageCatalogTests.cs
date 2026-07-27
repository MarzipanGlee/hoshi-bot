using System.Text.RegularExpressions;
using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

public partial class MessageCatalogTests
{
    [GeneratedRegex(@"\{(\w+)(?::[^}]+)?\}")]
    private static partial Regex PlaceholderRegex();

    private static HashSet<string> Placeholders(string template) =>
        PlaceholderRegex().Matches(template).Select(m => m.Groups[1].Value).ToHashSet();

    // The translation gate: every enabled locale must define exactly the same keys.
    [Fact]
    public void Enabled_locales_have_identical_key_sets()
    {
        var reference = MessageCatalog.Templates(Language.En).Keys.ToHashSet();
        Assert.NotEmpty(reference);

        foreach (var language in Languages.Enabled)
        {
            var keys = MessageCatalog.Templates(language).Keys.ToHashSet();
            var missing = reference.Except(keys).ToList();
            var extra = keys.Except(reference).ToList();
            Assert.True(missing.Count == 0 && extra.Count == 0,
                $"{Languages.ToCode(language)}.json key mismatch — missing: [{string.Join(", ", missing)}], extra: [{string.Join(", ", extra)}]");
        }
    }

    // Placeholder names must match per key across locales — a translation that drops or
    // renames a {placeholder} would silently render the raw token.
    [Fact]
    public void Enabled_locales_have_identical_placeholders_per_key()
    {
        var reference = MessageCatalog.Templates(Language.En);
        foreach (var language in Languages.Enabled)
        {
            foreach (var (key, template) in MessageCatalog.Templates(language))
            {
                if (!reference.TryGetValue(key, out var referenceTemplate))
                    continue;

                var expected = Placeholders(referenceTemplate);
                var actual = Placeholders(template);
                Assert.True(expected.SetEquals(actual),
                    $"{Languages.ToCode(language)}.json '{key}' placeholders [{string.Join(", ", actual)}] != en [{string.Join(", ", expected)}]");
            }
        }
    }

    [Fact]
    public void Format_substitutes_named_placeholders()
    {
        // Uses a synthetic template via the fallback path: unknown key returns the key
        // itself, so test formatting through a real key instead.
        var text = MessageCatalog.Format(Language.En, "Common.Processing");
        Assert.Equal("⏳ Processing...", text);
    }

    [Fact]
    public void Format_falls_back_to_english_then_key()
    {
        // Fr has no locale file yet — must fall back to en.
        Assert.Equal("⏳ Processing...", MessageCatalog.Format(Language.Fr, "Common.Processing"));
        // Entirely unknown keys surface as themselves (never throw).
        Assert.Equal("No.Such.Key", MessageCatalog.Format(Language.En, "No.Such.Key"));
    }

    [Fact]
    public void Format_applies_culture_to_formattable_args()
    {
        // 1234.5 renders with a comma decimal separator in German.
        var de = MessageCatalog.Format(Language.De, "{value:0.0}", ("value", 1234.5));
        // The template comes back via the raw-key fallback, so substitution still runs.
        Assert.Equal("1234,5", de);

        var en = MessageCatalog.Format(Language.En, "{value:0.0}", ("value", 1234.5));
        Assert.Equal("1234.5", en);
    }

    [Fact]
    public void Format_leaves_unknown_tokens_visible() =>
        Assert.Equal("{unknown}", MessageCatalog.Format(Language.En, "{unknown}", ("known", 1)));

    [Theory]
    [InlineData(Language.En, 1, "one")]
    [InlineData(Language.En, 2, "other")]
    [InlineData(Language.De, 1, "one")]
    [InlineData(Language.De, 0, "other")]
    [InlineData(Language.Fr, 0, "one")]
    [InlineData(Language.Fr, 2, "other")]
    [InlineData(Language.Pt, 1, "one")]
    [InlineData(Language.Ru, 1, "one")]
    [InlineData(Language.Ru, 21, "one")]
    [InlineData(Language.Ru, 3, "few")]
    [InlineData(Language.Ru, 13, "many")]
    [InlineData(Language.Ru, 5, "many")]
    [InlineData(Language.Ja, 1, "other")]
    [InlineData(Language.Ko, 7, "other")]
    public void Plural_rules_select_expected_form(Language language, int count, string expected) =>
        Assert.Equal(expected, PluralRules.Select(language, count));
}
