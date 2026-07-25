using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace HoshiBot.Web.Services;

// Fetches the live territory-ownership feed from stfc.pro (https://api.stfc.pro/stfc_territories — the
// same feed territory.lol uses) and hands it to StfcTerritoryOwnershipImportService's upsert. Isolates
// the network fetch so the importer stays a pure entries→DB upsert usable by the file-upload page too.
// The feed is a flat array of { server, territory, tag, region }; tag is null for an unowned territory.
public class StfcTerritoryOwnershipSyncService(
    IHttpClientFactory httpClientFactory,
    StfcTerritoryOwnershipImportService importService)
{
    public async Task<StfcTerritoryOwnershipImportResult> SyncFromSourceAsync()
    {
        var client = httpClientFactory.CreateClient(nameof(StfcTerritoryOwnershipSyncService));
        var feed = await client.GetFromJsonAsync<List<FeedEntry>>("stfc_territories")
            ?? throw new InvalidOperationException("stfc.pro territories feed returned null.");

        var entries = feed
            .Select(e => new StfcTerritoryOwnershipImportEntry(e.Server, e.Territory, e.Tag))
            .ToList();

        return await importService.ImportAsync(entries);
    }

    private record FeedEntry(
        [property: JsonPropertyName("server")] int Server,
        [property: JsonPropertyName("territory")] int Territory,
        [property: JsonPropertyName("tag")] string? Tag);
}
