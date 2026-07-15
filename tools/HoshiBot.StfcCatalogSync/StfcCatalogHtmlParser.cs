using System.Text.Json;
using System.Text.RegularExpressions;

namespace HoshiBot.StfcCatalogSync;

public record ParsedServerCard(string Name, string Region, int Number, string? VeilGroupCode, string? InviteUrl);

// Extracts the server catalog from a static HTML snapshot of stfc.pro's server-overview page
// (manually saved by a human — see hoshi-bot-dotnet/data/, never fetched live).
//
// The page is a Next.js app-router document that streams its data as JSON inside the RSC
// payload (self.__next_f.push(...)). We read that embedded JSON directly rather than scraping
// the rendered card markup: the old approach was coupled to the page's Tailwind class names
// (text-blue-400 text-xl, text-white/70, …) and would silently break on any visual restyle.
// The JSON only changes if the source renames its data fields, which is far rarer.
//
// Two flat record shapes are pulled and joined on the server number:
//   server:  {"serverid":164,"name":"Mindmeld","region":"EU","regionnumber":2,"groupname":"EU-4",…}
//   invite:  {"id":1,"serverId":8,"discordUrl":"https://discord.gg/…","createdAt":…}
//
// In the raw file every JSON quote is backslash-escaped (\"), so each object is matched in that
// escaped form and then un-escaped by letting System.Text.Json reverse the string-escaping
// (wrap the match in quotes, deserialize to string) before it's parsed as JSON.
public static partial class StfcCatalogHtmlParser
{
    // Anchored on "serverid" immediately followed by "name" so it matches only the server-stats
    // records, not the site's other serverid-bearing objects. Flat objects → [^{}]* to the close.
    [GeneratedRegex(@"\{\\""serverid\\"":\d+,\\""name\\"":[^{}]*\}")]
    private static partial Regex ServerRecordRegex();

    // Per-server Discord invite record — a parallel array joined on serverId.
    [GeneratedRegex(@"\{\\""id\\"":\d+,\\""serverId\\"":\d+,\\""discordUrl\\"":[^{}]*\}")]
    private static partial Regex InviteRecordRegex();

    public static List<ParsedServerCard> Parse(string html)
    {
        var inviteByServer = new Dictionary<int, string>();
        foreach (Match m in InviteRecordRegex().Matches(html))
        {
            using var doc = JsonDocument.Parse(UnescapeRscObject(m.Value));
            var root = doc.RootElement;
            if (root.TryGetProperty("serverId", out var sid) && sid.TryGetInt32(out var serverId)
                && root.TryGetProperty("discordUrl", out var url) && url.ValueKind == JsonValueKind.String
                && url.GetString() is { Length: > 0 } inviteUrl)
            {
                inviteByServer[serverId] = inviteUrl;
            }
        }

        var results = new List<ParsedServerCard>();
        var seen = new HashSet<int>();
        foreach (Match m in ServerRecordRegex().Matches(html))
        {
            using var doc = JsonDocument.Parse(UnescapeRscObject(m.Value));
            var root = doc.RootElement;

            if (!root.TryGetProperty("serverid", out var idEl) || !idEl.TryGetInt32(out var number)
                || !root.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("region", out var regionEl) || regionEl.ValueKind != JsonValueKind.String)
                continue;

            // Dedupe within a file; Program.cs also dedupes across files (last file wins).
            if (!seen.Add(number))
                continue;

            var veilGroupCode = root.TryGetProperty("groupname", out var group) && group.ValueKind == JsonValueKind.String
                ? group.GetString()
                : null;
            inviteByServer.TryGetValue(number, out var invite);

            results.Add(new ParsedServerCard(nameEl.GetString()!, regionEl.GetString()!, number, veilGroupCode, invite));
        }

        return results;
    }

    // The match is a JSON object exactly as it sits *inside* the RSC payload string, so every
    // quote and backslash is JSON-string-escaped. Wrapping it in quotes and deserializing as a
    // string reverses precisely that escaping (\" \\ \/ \uXXXX …) and yields real JSON text.
    private static string UnescapeRscObject(string escaped) =>
        JsonSerializer.Deserialize<string>("\"" + escaped + "\"")!;
}
