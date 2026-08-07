using System.Text.RegularExpressions;
using HoshiBot.Domain;
using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

// Guards the icon registry (Icons) and the "{icon:Name}" placeholder that lets locale files
// reference it instead of embedding their own copy of a symbol.
public partial class IconsTests
{
    [GeneratedRegex(@"\{icon:([^}]+)\}")]
    private static partial Regex IconPlaceholderRegex();

    // The failure this prevents: a renamed or deleted Icons member leaves the locale files pointing
    // at nothing, and MessageCatalog deliberately renders an unknown token verbatim rather than
    // silently blanking it — so members would read a literal "{icon:Warning}" in a DM. Nothing else
    // links the two, because the reference lives in JSON.
    [Fact]
    public void Every_icon_placeholder_in_every_locale_resolves()
    {
        foreach (var language in Languages.Enabled)
        {
            foreach (var (key, template) in MessageCatalog.Templates(language))
            {
                foreach (Match match in IconPlaceholderRegex().Matches(template))
                {
                    var name = match.Groups[1].Value;
                    Assert.True(MessageCatalog.ResolveIcon(name) is not null,
                        $"{Languages.ToCode(language)}/{key} references unknown icon \"{name}\".");
                }
            }
        }
    }

    [Fact]
    public void Placeholders_are_replaced_with_the_registry_value()
    {
        var rendered = MessageCatalog.Format(Language.En, "Common.Processing");

        Assert.StartsWith(Icons.Pending, rendered);
        Assert.DoesNotContain("{icon:", rendered);
    }

    // A template with no caller arguments still has to go through substitution — the icon pass would
    // otherwise be skipped by the "nothing to format" shortcut Format used to take.
    [Fact]
    public void Icons_resolve_even_when_no_arguments_are_passed()
    {
        Assert.DoesNotContain("{icon:", MessageCatalog.Format(Language.De, "Say.NoRoleConfigured"));
    }

    [Fact]
    public void Nested_groups_are_addressable_by_their_qualified_name()
    {
        Assert.Equal(Icons.Text.Check, MessageCatalog.ResolveIcon("Text.Check"));
        Assert.Equal(Icons.Ok, MessageCatalog.ResolveIcon("Ok"));
        Assert.Null(MessageCatalog.ResolveIcon("Check"));
    }

    // Discord and the Web want different presentations of the same idea, which is why Icons.Text
    // exists at all: those glyphs must stay text-presentation (no U+FE0F), or a browser swaps them
    // for full-colour emoji and the dense admin tables turn loud.
    [Fact]
    public void Text_glyphs_carry_no_variation_selector()
    {
        foreach (var (name, value) in MessageCatalog.IconsByName.Where(i => i.Key.StartsWith("Text.")))
            Assert.False(value.Contains('️'), $"Icons.{name} is emoji-presentation but lives in the Text group.");
    }

    // Two names may deliberately share a value (Blocked and RoeViolation), but a name must never be
    // empty or stray whitespace — that renders as a missing icon nobody notices.
    [Fact]
    public void Every_registry_entry_has_a_value()
    {
        Assert.NotEmpty(MessageCatalog.IconsByName);
        foreach (var (name, value) in MessageCatalog.IconsByName)
            Assert.False(string.IsNullOrWhiteSpace(value), $"Icons.{name} is empty.");
    }
}
