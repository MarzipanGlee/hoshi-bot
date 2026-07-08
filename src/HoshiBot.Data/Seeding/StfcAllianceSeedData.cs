using System.Reflection;
using System.Text.Json;

namespace HoshiBot.Data.Seeding;

// A one-time snapshot of every server's alliance roster (ExternalId, ServerId, Tag, Name),
// captured 2026-07-08 from an external STFC stats site's API. Unlike StfcCatalogSeedData,
// this isn't auto-regenerated — alliance rosters change constantly (renames, disbands,
// server moves), so this is just a starting point seeded once into an empty table; ongoing
// changes are expected to go through the existing Manage > STFC Catalog > Alliances admin
// pages, not a re-run of this file.
//
// Stored as an embedded JSON resource rather than a C# array literal — at ~10,000 rows,
// the literal was adding real Roslyn compile time to every single build (this class is
// compiled into all three deployed images). A one-time file read + deserialize at first
// access costs nothing by comparison.
public static class StfcAllianceSeedData
{
    private record Entry(long ExternalId, int ServerId, string Tag, string Name);

    public static readonly (long ExternalId, int ServerId, string Tag, string Name)[] Entries = Load();

    private static (long ExternalId, int ServerId, string Tag, string Name)[] Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "HoshiBot.Data.Seeding.StfcAllianceSeedData.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var entries = JsonSerializer.Deserialize<Entry[]>(stream)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' deserialized to null.");

        return entries.Select(e => (e.ExternalId, e.ServerId, e.Tag, e.Name)).ToArray();
    }
}
