using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HoshiBot.StfcSeedSync;

// Regenerates the embedded STFC seed JSON under src/HoshiBot.Data/Seeding/ from the raw
// snapshots under data/ (gitignored). One command per refresh: drop fresh files in data/, run
// `dotnet run --project tools/HoshiBot.StfcSeedSync` from the repo root, commit Seeding/.
//
// data/ is the full-raw working store; Seeding/ holds the shipped, embedded seed. Small feeds
// are stored raw and the loaders project; the two heavy sources (alliances, players) are
// trimmed to the fields the seeders use to keep the embedded data small. The player pages
// arrive gzipped + paginated, so they are also merged into a single full-raw data/players.json.
//
// Not handled here (deliberate): events.json — StfcEventStatusSeedData is hand-curated
// (region-split Incursions times the feed does not carry) and must not be overwritten;
// schedules.json — fetch-cadence metadata with no seed; territory/*.svg — territory
// definitions, a separate concern.

var dataDir = args.Length > 0 ? args[0] : "data";
var seedingDir = Path.Combine("src", "HoshiBot.Data", "Seeding");

if (!Directory.Exists(dataDir))
{
    Console.Error.WriteLine($"Input directory not found: {dataDir}. Run from the repo root, or pass the data dir as an argument.");
    return 1;
}
if (!Directory.Exists(seedingDir))
{
    Console.Error.WriteLine($"Seeding directory not found: {seedingDir}. Run this tool from the repo root.");
    return 1;
}

// UnsafeRelaxed so non-ASCII names/tags (e.g. "DFÖ", Cyrillic) stay as readable UTF-8 rather
// than \uXXXX escapes; compact (no indentation) to match the other embedded seed files.
var writeOpts = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var readOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

SyncPlayers();
SyncAlliances();
SyncServers();
SyncRawFeed("server-status.json", "StfcServerStatusSeedData.json");
SyncRawFeed("territory-owners.json", "StfcTerritoryOwnershipSeedData.json");

Console.WriteLine("Done.");
return 0;

// players_164_* (gzip pages, {count,players:[{data:{…}}]}) → merged full-raw data/players.json
// → trimmed Seeding/StfcPlayerSeedData.json.
void SyncPlayers()
{
    var pages = Directory.GetFiles(dataDir, "players_164_*")
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToList();
    if (pages.Count == 0)
    {
        Console.WriteLine("players: no players_164_* pages found — skipping.");
        return;
    }

    var records = new List<JsonElement>();
    foreach (var page in pages)
    {
        using var fs = File.OpenRead(page);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gz);
        foreach (var player in doc.RootElement.GetProperty("players").EnumerateArray())
            records.Add(player.GetProperty("data").Clone());
    }

    File.WriteAllText(Path.Combine(dataDir, "players.json"), JsonSerializer.Serialize(records, writeOpts));

    var seed = records.Select(d => new PlayerSeed(
        d.GetProperty("playerid").GetInt64(),
        d.GetProperty("owner").GetString()!,
        d.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.String && tag.GetString() is { Length: > 0 } t ? t : null))
        .ToArray();
    WriteSeed("StfcPlayerSeedData.json", seed);
    Console.WriteLine($"players: {pages.Count} pages → {records.Count} players (data/players.json + trimmed seed).");
}

// data/alliances (one big JSON dump, with stray control chars in slogan/announcement) → trimmed
// Seeding/StfcAllianceSeedData.json.
void SyncAlliances()
{
    var path = Path.Combine(dataDir, "alliances");
    if (!File.Exists(path))
    {
        Console.WriteLine("alliances: data/alliances not found — skipping.");
        return;
    }

    var alliances = JsonSerializer.Deserialize<AllianceRaw[]>(BlankControlChars(File.ReadAllText(path)), readOpts)
        ?? throw new InvalidOperationException("data/alliances deserialized to null.");

    var seed = alliances.Select(a => new AllianceSeed(a.Id, a.Server, a.Tag, a.Name, a.Emblem)).ToArray();
    WriteSeed("StfcAllianceSeedData.json", seed);
    Console.WriteLine($"alliances: {seed.Length} → trimmed seed.");
}

// The alliance dump carries raw control characters inside string values (slogan/announcement),
// which are invalid JSON. Those fields are dropped by the projection, so blanking every control
// char before parsing is harmless (also flattens any structural pretty-print whitespace).
static string BlankControlChars(string text)
{
    var sb = new StringBuilder(text.Length);
    foreach (var ch in text)
        sb.Append(ch < ' ' ? ' ' : ch);
    return sb.ToString();
}

// data/servers.htm (and any other data/*.htm) → two raw Seeding files (server records + invite
// records), deduped by server number (last file wins, so servers.htm is authoritative).
void SyncServers()
{
    var htmlFiles = Directory.GetFiles(dataDir, "*.htm")
        .Concat(Directory.GetFiles(dataDir, "*.html"))
        .OrderBy(f => f, StringComparer.Ordinal)
        .ToList();
    if (htmlFiles.Count == 0)
    {
        Console.WriteLine("servers: no *.htm files found — skipping.");
        return;
    }

    var serversByNumber = new Dictionary<int, JsonElement>();
    var invitesByServer = new Dictionary<int, JsonElement>();
    foreach (var file in htmlFiles)
    {
        var html = File.ReadAllText(file);
        foreach (var server in ServersHtmlExtractor.ExtractServerRecords(html))
            serversByNumber[server.GetProperty("serverid").GetInt32()] = server;
        foreach (var invite in ServersHtmlExtractor.ExtractInviteRecords(html))
            invitesByServer[invite.GetProperty("serverId").GetInt32()] = invite;
    }

    WriteSeed("StfcServerSeedData.json", serversByNumber.Values);
    WriteSeed("StfcServerInviteSeedData.json", invitesByServer.Values);
    Console.WriteLine($"servers: {serversByNumber.Count} servers, {invitesByServer.Count} invites → two raw seed files.");
}

// A plain-JSON feed copied verbatim (re-serialized to validate + normalize) into Seeding/.
void SyncRawFeed(string sourceName, string outputName)
{
    var path = Path.Combine(dataDir, sourceName);
    if (!File.Exists(path))
    {
        Console.WriteLine($"{sourceName}: not found — skipping.");
        return;
    }

    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    WriteSeed(outputName, doc.RootElement);
    Console.WriteLine($"{sourceName} → {outputName} (raw).");
}

void WriteSeed<T>(string name, T value) =>
    File.WriteAllText(Path.Combine(seedingDir, name), JsonSerializer.Serialize(value, writeOpts));

// Trimmed seed shapes (PascalCase keys — must match the loaders in HoshiBot.Data/Seeding).
internal record AllianceSeed(long ExternalId, int ServerId, string Tag, string Name, int Emblem);
internal record PlayerSeed(long ExternalId, string Name, string? AllianceTag);

// Raw alliance record fields we keep (case-insensitive read; the dump has many more).
internal record AllianceRaw(long Id, int Server, string Tag, string Name, int Emblem);
