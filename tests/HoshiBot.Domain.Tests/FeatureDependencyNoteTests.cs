using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

public class FeatureDependencyNoteTests
{
    // HasNote: true is a promise that a note exists. Break it and the Features page renders the
    // dependency followed by a bare em dash and nothing else — which is how Alliance Tag Roles
    // shipped, since Msg.WebGuild.DependencyNote turns a missing key into "" rather than showing
    // the key.
    //
    // The existing catalog suites couldn't catch it: they compare locales AGAINST EACH OTHER, and a
    // key missing from both sides matches. Only walking the declarations finds it.
    [Fact]
    public void Every_declared_note_has_text_in_every_locale()
    {
        var missing = new List<string>();

        foreach (var feature in Enum.GetValues<GuildFeature>())
        {
            foreach (var dependency in GuildFeatureDependencies.Of(feature).Where(d => d.HasNote))
            {
                foreach (var language in Languages.Enabled)
                {
                    if (string.IsNullOrWhiteSpace(Msg.WebGuild.DependencyNote(language, feature, dependency.Feature)))
                        missing.Add($"{Languages.ToCode(language)}: {feature} -> {dependency.Feature}");
                }
            }
        }

        Assert.True(missing.Count == 0,
            $"Dependencies declared HasNote: true with no note text:\n  {string.Join("\n  ", missing)}");
    }

    // The other direction: a note nobody asks for is dead weight, and usually means HasNote was
    // dropped from the declaration while the string stayed behind.
    [Fact]
    public void Every_note_in_the_catalog_is_declared()
    {
        var declared = Enum.GetValues<GuildFeature>()
            .SelectMany(f => GuildFeatureDependencies.Of(f).Where(d => d.HasNote).Select(d => $"Web.Guild.DependencyNote.{f}.{d.Feature}"))
            .ToHashSet();

        var orphaned = MessageCatalog.Templates(Language.En).Keys
            .Where(k => k.StartsWith("Web.Guild.DependencyNote.") && !declared.Contains(k))
            .ToList();

        Assert.True(orphaned.Count == 0, $"Notes with no HasNote declaration: {string.Join(", ", orphaned)}");
    }
}
