using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HoshiBot.Domain.Localization;

namespace HoshiBot.Domain.Tests;

// The gap the other MessageCatalog tests leave open: they compare the locales against EACH OTHER,
// so a key that is missing from *both* passes every one of them. That is the easy mistake to make —
// you add a Msg accessor, forget the catalog entry, and the UI renders the raw key
// ("Web.Audit.ReauthorizeOptionalButton") in every language. Which is exactly what shipped once.
//
// So: read the Msg.*.cs sources and assert every literal key they ask for actually exists.
public partial class MessageCatalogKeyUsageTests
{
    // Only literal keys — the interpolated ones (Msg.WebFeature.Title's $"Web.Feature.{feature}.Title",
    // Msg.WebAudit.Perm/Profile, …) can't be checked this way and all carry their own fallback so a
    // miss degrades to a readable name rather than a raw key.
    [GeneratedRegex("""MessageCatalog\.(Format|FormatCount)\(\s*lang(?:uage)?\s*,\s*"([^"]+)"    """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex CatalogCallRegex();

    [Fact]
    public void Every_literal_key_a_Msg_accessor_asks_for_exists_in_the_catalog()
    {
        var templates = MessageCatalog.Templates(Language.En);
        Assert.NotEmpty(templates);

        var missing = new List<string>();
        var files = Directory.GetFiles(LocalizationSourceDirectory(), "Msg.*.cs");
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            foreach (Match match in CatalogCallRegex().Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[2].Value;

                // FormatCount resolves a plural suffix rather than the bare key (PluralRules).
                var found = match.Groups[1].Value == "FormatCount"
                    ? templates.ContainsKey($"{key}.one") && templates.ContainsKey($"{key}.other")
                    : templates.ContainsKey(key);

                if (!found)
                    missing.Add($"{Path.GetFileName(file)}: {key}");
            }
        }

        Assert.True(missing.Count == 0, $"Catalog keys referenced but never defined:\n  {string.Join("\n  ", missing)}");
    }

    // Located from this file's own compile-time path so the test doesn't depend on the working
    // directory or the bin layout.
    private static string LocalizationSourceDirectory([CallerFilePath] string thisFile = "")
    {
        var testsDirectory = Path.GetDirectoryName(thisFile)!;
        var repoRoot = Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
        return Path.Combine(repoRoot, "src", "HoshiBot.Domain", "Localization");
    }
}
