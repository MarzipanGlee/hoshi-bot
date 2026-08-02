using System.Reflection;
using System.Text.Json;

namespace HoshiBot.Data.Seeding;

// A one-time snapshot of the player rosters of the servers we hold pages for (ExternalId, Name,
// AllianceTag, Server), captured from an external STFC stats site's API (same source/caveats as
// StfcAllianceSeedData). AllianceTag is null for unaffiliated players or ones whose tag didn't
// match a seeded alliance at import time — not re-checked here, the seeder resolves it against
// StfcAlliances at seed time instead.
//
// Server is per row rather than a constant on this class: the file used to hold one server and
// exposed a Server164Entries/Server164Id pair, which meant a second server's pages had nowhere to
// go (and were quietly ignored by the sync tool for a while). Adding a server is now just dropping
// its players_{server}_* pages into data/ and re-running tools/HoshiBot.StfcSeedSync.
//
// Stored as an embedded JSON resource, not a C# array literal — same reasoning as
// StfcAllianceSeedData (~1,500 rows was real Roslyn compile time on every build).
public static class StfcPlayerSeedData
{
    private record Entry(long ExternalId, string Name, string? AllianceTag, int Server, int? RankId, int? OpsLevel);

    public static readonly (long ExternalId, string Name, string? AllianceTag, int Server, int? RankId, int? OpsLevel)[] Entries = Load();

    // The servers this snapshot covers — the seeder walks these so it can skip one that already
    // has players without skipping the rest.
    public static IEnumerable<int> Servers => Entries.Select(e => e.Server).Distinct();

    private static (long ExternalId, string Name, string? AllianceTag, int Server, int? RankId, int? OpsLevel)[] Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "HoshiBot.Data.Seeding.StfcPlayerSeedData.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var entries = JsonSerializer.Deserialize<Entry[]>(stream)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' deserialized to null.");

        return entries.Select(e => (e.ExternalId, e.Name, e.AllianceTag, e.Server, e.RankId, e.OpsLevel)).ToArray();
    }
}
