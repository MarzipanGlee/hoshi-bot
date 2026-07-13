using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoshiBot.Data.Seeding;

// Per-server territory ownership snapshot from stfc.pro's stfc_territories API (the feed
// behind territory.lol): one row per (server, territory) with the owning alliance's Tag, or a
// null Tag for an unowned territory. Copied verbatim as an embedded JSON resource — same
// approach as StfcAllianceSeedData/StfcPlayerSeedData, and for the same reason (thousands of
// rows compile faster as a file read than a C# array literal). Seeded once into an empty
// StfcTerritoryOwnerships table by SeedStfcTerritoryOwnershipIfEmptyAsync, which resolves each
// (Server, Tag) against StfcAlliances; null-Tag rows and any Tag/Territory it can't resolve are
// skipped. This is the authoritative Unicode source for owning tags (Cyrillic/special-Latin
// tags come through correctly, unlike the hand-transcribed snapshot it replaced).
public static class StfcTerritoryOwnershipSeedData
{
    private record Entry(
        [property: JsonPropertyName("server")] int Server,
        [property: JsonPropertyName("territory")] int Territory,
        [property: JsonPropertyName("tag")] string? Tag);

    public static readonly (int Server, int Territory, string? Tag)[] Entries = Load();

    private static (int Server, int Territory, string? Tag)[] Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "HoshiBot.Data.Seeding.StfcTerritoryOwnershipSeedData.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var entries = JsonSerializer.Deserialize<Entry[]>(stream)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' deserialized to null.");

        return entries.Select(e => (e.Server, e.Territory, e.Tag)).ToArray();
    }
}
