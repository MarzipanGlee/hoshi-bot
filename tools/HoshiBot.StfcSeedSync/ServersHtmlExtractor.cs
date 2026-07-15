using System.Text.Json;
using System.Text.RegularExpressions;

namespace HoshiBot.StfcSeedSync;

// Extracts the two raw JSON record shapes from a static HTML snapshot of stfc.pro's
// server-overview page (manually saved by a human — see data/servers.htm, never fetched live).
//
// The page is a Next.js app-router document that streams its data as JSON inside the RSC
// payload (self.__next_f.push(...)). We read that embedded JSON directly rather than scraping
// the rendered card markup — the old approach was coupled to Tailwind class names and would
// silently break on any restyle; the JSON only changes if the source renames its fields.
//
//   server:  {"serverid":164,"name":"Mindmeld","region":"EU","regionnumber":2,"groupname":"EU-4",…}
//   invite:  {"id":1,"serverId":8,"discordUrl":"https://discord.gg/…","createdAt":…}
//
// In the raw file every JSON quote is backslash-escaped (\"), so each object is matched in that
// escaped form and then un-escaped by letting System.Text.Json reverse the string-escaping
// (wrap the match in quotes, deserialize to string) before it is parsed as JSON. The objects
// are returned verbatim (cloned) so the caller can persist them raw and project later.
public static partial class ServersHtmlExtractor
{
    // Anchored on "serverid" immediately followed by "name" so it matches only the server-stats
    // records, not the site's other serverid-bearing objects. Flat objects → [^{}]* to the close.
    [GeneratedRegex(@"\{\\""serverid\\"":\d+,\\""name\\"":[^{}]*\}")]
    private static partial Regex ServerRecordRegex();

    // Per-server Discord invite record — a parallel array joined on serverId.
    [GeneratedRegex(@"\{\\""id\\"":\d+,\\""serverId\\"":\d+,\\""discordUrl\\"":[^{}]*\}")]
    private static partial Regex InviteRecordRegex();

    public static List<JsonElement> ExtractServerRecords(string html) => Extract(html, ServerRecordRegex());

    public static List<JsonElement> ExtractInviteRecords(string html) => Extract(html, InviteRecordRegex());

    private static List<JsonElement> Extract(string html, Regex regex)
    {
        var results = new List<JsonElement>();
        foreach (Match m in regex.Matches(html))
        {
            using var doc = JsonDocument.Parse(UnescapeRscObject(m.Value));
            results.Add(doc.RootElement.Clone());
        }
        return results;
    }

    // The match is a JSON object exactly as it sits *inside* the RSC payload string, so every
    // quote and backslash is JSON-string-escaped. Wrapping it in quotes and deserializing as a
    // string reverses precisely that escaping (\" \\ \/ \uXXXX …) and yields real JSON text.
    private static string UnescapeRscObject(string escaped) =>
        JsonSerializer.Deserialize<string>("\"" + escaped + "\"")!;
}
