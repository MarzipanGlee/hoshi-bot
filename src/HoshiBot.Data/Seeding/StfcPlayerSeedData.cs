using System.Reflection;
using System.Text.Json;

namespace HoshiBot.Data.Seeding;

// A one-time snapshot of server 164's player roster (ExternalId, Name, AllianceTag),
// captured 2026-07-08 from an external STFC stats site's API (same source/caveats as
// StfcAllianceSeedData). AllianceTag is null for unaffiliated players or ones whose tag
// didn't match a seeded alliance at import time — not re-checked here, the seeder
// resolves it against StfcAlliances at seed time instead.
//
// Stored as an embedded JSON resource, not a C# array literal — same reasoning as
// StfcAllianceSeedData (~1,500 rows was real Roslyn compile time on every build).
public static class StfcPlayerSeedData
{
    public const int Server164Id = 164;

    private record Entry(long ExternalId, string Name, string? AllianceTag);

    public static readonly (long ExternalId, string Name, string? AllianceTag)[] Server164Entries = Load();

    private static (long ExternalId, string Name, string? AllianceTag)[] Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "HoshiBot.Data.Seeding.StfcPlayerSeedData.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var entries = JsonSerializer.Deserialize<Entry[]>(stream)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' deserialized to null.");

        return entries.Select(e => (e.ExternalId, e.Name, e.AllianceTag)).ToArray();
    }
}
