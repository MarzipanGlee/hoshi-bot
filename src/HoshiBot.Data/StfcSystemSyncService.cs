using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Data;

// Keeps StfcSystem in sync with stfc.space's own data feeds. Scheduled by
// StfcSystemAutoSyncService (HoshiBot.Web) — on startup if the table is empty, then once a
// day. No robots.txt disallows these endpoints (unlike stfc.pro), and stfc.space is now
// operated by Scopely (the game's own developer), not a fan project with separate usage terms.
//
// Two endpoints are combined:
//   - system/summary.json: the authoritative list of real system IDs (2,596 of them), with
//     structured gameplay fields (coordinates, faction, hazard level, has_player_containers,
//     etc. — only has_player_containers is used here, the rest aren't needed yet). This is
//     what stfc.space's own /systems page loads (traced via its bundled JS: a
//     getSummary("/system") call), not the translations file below. has_player_containers
//     is what drives that page's "Housing" badge (also confirmed via the bundled JS —
//     e.item.has_player_containers gates the housing icon), i.e. STFC's "Station Housing".
//   - translations/en/systems.json: display names, keyed by the same numeric id. On its
//     own this file is unusable for identifying systems — it bundles far more than star
//     systems under the "title" key (generic in-system point-of-interest labels like
//     "Corrupted Data Accelerator" or "Mirror Common Warp Zone" that repeat across dozens
//     of systems, markup-wrapped hostile-fleet names, and "."/empty placeholders — 14,976
//     raw entries for 2,596 real systems). Joining against summary.json's id set is exact,
//     unlike guessing from name-repetition frequency.
public partial class StfcSystemSyncService(
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<StfcSystemSyncService> logger)
{
    private const string SystemSummaryUrl = "https://data.stfc.space/system/summary.json";
    private const string SystemNamesUrl = "https://data.stfc.space/translations/en/systems.json";

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex RichTextTagRegex();

    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return !await db.StfcSystems.AnyAsync(cancellationToken);
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(StfcSystemSyncService));

            var summaries = await client.GetFromJsonAsync<List<SystemSummaryEntry>>(SystemSummaryUrl, cancellationToken);
            var translations = await client.GetFromJsonAsync<List<TranslationEntry>>(SystemNamesUrl, cancellationToken);
            if (summaries is null || translations is null)
            {
                logger.LogWarning("STFC system sync: received no data from stfc.space");
                return;
            }

            var namesById = translations
                .Where(t => t.Key == "title")
                .ToDictionary(t => t.Id, t => CleanName(t.Text));

            var systems = new List<(int Number, string Name, bool HasStationHousing)>();
            var missingNames = 0;
            foreach (var summary in summaries)
            {
                if (!namesById.TryGetValue(summary.Id.ToString(), out var name) || name.Length == 0)
                {
                    missingNames++;
                    continue;
                }

                systems.Add((summary.Id, name, summary.HasPlayerContainers));
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var existingByNumber = await db.StfcSystems.ToDictionaryAsync(s => s.Number, cancellationToken);

            var added = 0;
            var renamed = 0;
            foreach (var (number, name, hasStationHousing) in systems)
            {
                if (existingByNumber.TryGetValue(number, out var existing))
                {
                    if (existing.Name != name)
                    {
                        existing.Name = name;
                        renamed++;
                    }

                    existing.HasStationHousing = hasStationHousing;
                }
                else
                {
                    db.StfcSystems.Add(new StfcSystem { Number = number, Name = name, HasStationHousing = hasStationHousing });
                    added++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            var linked = await ReconcileTerritoryLinksAsync(db, cancellationToken);

            logger.LogInformation(
                "STFC system sync complete: {Total} systems ({Added} added, {Renamed} renamed, {Missing} missing a name, {Linked} territory links updated)",
                systems.Count, added, renamed, missingNames, linked);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "STFC system sync failed");
        }
    }

    // A zone groups 2-4 real systems named "{ZoneName} {Suffix}" (e.g. "Hoobishan Alpha"/
    // "Hoobishan Beta" for zone "Hoobishan") — this pattern is exact and mechanical, so the
    // link is derived here on every sync rather than hand-maintained as seed data. Runs
    // regardless of whether StfcTerritories was just seeded or already existed, so linkage
    // self-heals on the next daily sync no matter which of the two background
    // processes (this sync vs. the territory seeder) happens to run first.
    private static async Task<int> ReconcileTerritoryLinksAsync(HoshiBotDbContext db, CancellationToken cancellationToken)
    {
        var territories = await db.StfcTerritories.ToListAsync(cancellationToken);
        if (territories.Count == 0)
            return 0;

        var systems = await db.StfcSystems.ToListAsync(cancellationToken);
        var linked = 0;

        foreach (var system in systems)
        {
            var match = territories.FirstOrDefault(t => IsZoneSystem(system.Name, t.Name));
            var matchedId = match?.Id;
            if (system.TerritoryId == matchedId)
                continue;

            system.TerritoryId = matchedId;
            linked++;
        }

        if (linked > 0)
            await db.SaveChangesAsync(cancellationToken);

        return linked;
    }

    private static bool IsZoneSystem(string systemName, string territoryName)
    {
        if (!systemName.StartsWith(territoryName + " ", StringComparison.Ordinal))
            return false;

        var suffix = systemName[(territoryName.Length + 1)..];
        return suffix.Length > 0 && !suffix.Contains(' ');
    }

    // Some system names carry the game's own rich-text markup — e.g.
    // "<color=#E69453>[ELITE]</color> Karyndor" (42 systems) or
    // "Meeoden <color=#FF8080>&#128128;</color>" (24 systems, a hazard-marker skull emoji).
    // Strip the tags generically (not hardcoded to these two colors, in case more appear
    // later) and HTML-decode the remaining text so entity references render as real
    // characters instead of literal "&#128128;".
    private static string CleanName(string rawName) =>
        WebUtility.HtmlDecode(RichTextTagRegex().Replace(rawName, "")).Trim();

    private record SystemSummaryEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("has_player_containers")] bool HasPlayerContainers);

    private record TranslationEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("text")] string Text);
}
