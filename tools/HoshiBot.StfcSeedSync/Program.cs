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
// trimmed to the fields the seeders use to keep the embedded data small. Where a raw source
// arrives split/compressed/HTML-embedded rather than as plain JSON, this also writes a
// full-fidelity JSON copy back into data/ (not just the trimmed/raw Seeding output) so data/
// always holds a clean, complete, single-file version of everything: the gzipped, paginated
// player pages merge into data/players.json; the control-char-laden alliance dump becomes
// data/alliances.json; servers.htm's embedded RSC payload becomes data/servers.json +
// data/server-invites.json.
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

SyncPlayers();
SyncAlliances();
SyncServers();
SyncRawFeed("server-status.json", "StfcServerStatusSeedData.json");
SyncRawFeed("territory-owners.json", "StfcTerritoryOwnershipSeedData.json");

Console.WriteLine("Done.");
return 0;

// players_{server}_* (gzip pages, {count,players:[{data:{…}}]}) → merged full-raw
// data/players.json → trimmed Seeding/StfcPlayerSeedData.json.
//
// The server comes from the file name, so dropping in a new server's pages is all it takes to
// seed it. A players_* file whose name carries no server number is a hard error rather than a
// skip: the glob used to be pinned to one server, and 20 pages of another sat in data/ for a
// while being silently ignored.
void SyncPlayers()
{
    var pagesByServer = new SortedDictionary<int, List<string>>();
    foreach (var path in Directory.GetFiles(dataDir, "players_*").OrderBy(f => f, StringComparer.Ordinal))
    {
        // players_164_07 → 164. Anything else under players_* is a mistake worth stopping for.
        var parts = Path.GetFileName(path).Split('_');
        if (parts.Length < 3 || !int.TryParse(parts[1], out var serverId))
            throw new InvalidOperationException(
                $"Cannot read a server id from '{Path.GetFileName(path)}'. Player pages must be named players_{{server}}_{{page}}.");

        pagesByServer.TryAdd(serverId, []);
        pagesByServer[serverId].Add(path);
    }

    if (pagesByServer.Count == 0)
    {
        Console.WriteLine("players: no players_* pages found — skipping.");
        return;
    }

    var records = new List<JsonElement>();
    var seed = new List<PlayerSeed>();
    foreach (var (serverId, pages) in pagesByServer)
    {
        foreach (var page in pages)
        {
            using var fs = File.OpenRead(page);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var doc = JsonDocument.Parse(gz);
            foreach (var player in doc.RootElement.GetProperty("players").EnumerateArray())
            {
                var data = player.GetProperty("data").Clone();
                records.Add(data);
                seed.Add(new PlayerSeed(
                    data.GetProperty("playerid").GetInt64(),
                    data.GetProperty("owner").GetString()!,
                    data.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.String && tag.GetString() is { Length: > 0 } t ? t : null,
                    serverId));
            }
        }
    }

    File.WriteAllText(Path.Combine(dataDir, "players.json"), JsonSerializer.Serialize(records, writeOpts));
    WriteSeed("StfcPlayerSeedData.json", seed.ToArray());

    var perServer = string.Join(", ", pagesByServer.Select(kv => $"{kv.Key}: {kv.Value.Count} pages"));
    Console.WriteLine($"players: {perServer} → {records.Count} players (data/players.json + trimmed seed).");
}

// data/alliances (one big JSON dump, no file extension, with stray control chars in
// slogan/announcement) → a full-fidelity, validly-escaped data/alliances.json (every field,
// same idea as the merged data/players.json) + trimmed Seeding/StfcAllianceSeedData.json.
void SyncAlliances()
{
    var path = Path.Combine(dataDir, "alliances");
    if (!File.Exists(path))
    {
        Console.WriteLine("alliances: data/alliances not found — skipping.");
        return;
    }

    using var doc = JsonDocument.Parse(BlankControlChars(File.ReadAllText(path)));
    var records = doc.RootElement.EnumerateArray().ToList();

    File.WriteAllText(Path.Combine(dataDir, "alliances.json"), JsonSerializer.Serialize(records, writeOpts));

    var seed = records.Select(a => new AllianceSeed(
            a.GetProperty("id").GetInt64(), a.GetProperty("server").GetInt32(),
            a.GetProperty("tag").GetString()!, a.GetProperty("name").GetString()!, a.GetProperty("emblem").GetInt32()))
        .ToArray();
    WriteSeed("StfcAllianceSeedData.json", seed);
    Console.WriteLine($"alliances: {seed.Length} → data/alliances.json (full) + trimmed seed.");
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

// data/servers.htm (and any other data/*.htm) → two full-raw data/ files (server records +
// invite records, same idea as data/players.json / data/alliances.json) + the matching two raw
// Seeding files, deduped by server number (last file wins, so servers.htm is authoritative).
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

    var servers = serversByNumber.Values.ToList();
    var invites = invitesByServer.Values.ToList();

    WriteDataJson("servers.json", servers);
    WriteDataJson("server-invites.json", invites);
    WriteSeed("StfcServerSeedData.json", servers);
    WriteSeed("StfcServerInviteSeedData.json", invites);
    Console.WriteLine($"servers: {servers.Count} servers, {invites.Count} invites → data/servers.json + data/server-invites.json + seed files.");
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

void WriteDataJson<T>(string name, T value) =>
    File.WriteAllText(Path.Combine(dataDir, name), JsonSerializer.Serialize(value, writeOpts));

// Trimmed seed shapes (PascalCase keys — must match the loaders in HoshiBot.Data/Seeding).
internal record AllianceSeed(long ExternalId, int ServerId, string Tag, string Name, int Emblem);
internal record PlayerSeed(long ExternalId, string Name, string? AllianceTag, int Server);
