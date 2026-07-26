using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HoshiBot.Data;

// Manually-triggered synchronizer for Territory Capture service data from territory.lol. Populates
// the global service catalog (StfcTerritoryService) and the per-server territory→service mapping
// (StfcTerritoryServiceSlot), so the TC "Services" reminder can list each zone's actual services.
//
// Data chain (see the site's own script.js): meta.json gives (tcSeason, generatedAt) for
// change-detection; static_eu.json's territory_service_specs is the global catalog (svid → loca_id
// + rarity); translation.json resolves loca → name/description; service_slots_{serverId}.json maps
// each zone to its ordered list of service spec ids on that server. Only servers with a linked
// alliance are fetched.
public class TerritoryServiceSyncService(
    IHttpClientFactory httpClientFactory,
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    ILogger<TerritoryServiceSyncService> logger)
{
    public async Task<TerritoryServiceSyncResult> SyncAsync(bool force = false)
    {
        var client = httpClientFactory.CreateClient(nameof(TerritoryServiceSyncService));
        await using var db = await dbFactory.CreateDbContextAsync();

        var meta = await client.GetFromJsonAsync<MetaDto>("data/meta.json")
            ?? throw new InvalidOperationException("territory.lol meta.json returned null.");

        var state = await db.TerritoryServiceSyncStates.FirstOrDefaultAsync();
        if (!force && state is not null && state.TcSeason == meta.TcSeason && state.GeneratedAt == meta.GeneratedAt)
            return TerritoryServiceSyncResult.NoOp(meta.TcSeason, state.SyncedAt);

        // 1. Global service catalog: specs (svid → loca_id, rarity) + translations (loca → text).
        var staticData = await client.GetFromJsonAsync<StaticDto>("data/static_eu.json")
            ?? throw new InvalidOperationException("territory.lol static_eu.json returned null.");
        var translations = await client.GetFromJsonAsync<TranslationRootDto>("data/translation.json")
            ?? throw new InvalidOperationException("territory.lol translation.json returned null.");

        var textByKey = translations.Translations.TerritoryCapture
            .GroupBy(t => t.Key)
            .ToDictionary(g => g.Key, g => g.First().Text);
        string? Text(string prefix, int loca) => textByKey.GetValueOrDefault($"{prefix}_{loca}");

        var existingServices = await db.StfcTerritoryServices.ToDictionaryAsync(s => s.Id);
        var servicesUpserted = 0;

        foreach (var (svidStr, spec) in staticData.Specs)
        {
            if (!long.TryParse(svidStr, out var svid) || spec.IdRefs?.LocaId is not { } loca)
                continue;

            var name = Text("services_name", loca) is { Length: > 0 } n ? n : spec.Name ?? svidStr;

            if (existingServices.TryGetValue(svid, out var svc))
            {
                svc.LocaId = loca;
                svc.Name = name;
                svc.Description = Text("services_description", loca);
                svc.InfoShort = Text("services_info_short", loca);
                svc.Rarity = spec.Rarity;
            }
            else
            {
                db.StfcTerritoryServices.Add(new StfcTerritoryService
                {
                    Id = svid,
                    LocaId = loca,
                    Name = name,
                    Description = Text("services_description", loca),
                    InfoShort = Text("services_info_short", loca),
                    Rarity = spec.Rarity,
                });
            }
            servicesUpserted++;
        }

        await db.SaveChangesAsync();

        // 2. Per-server territory→service slots, for servers with a linked alliance only.
        var territoryIds = (await db.StfcTerritories.Select(t => t.Id).ToListAsync()).ToHashSet();
        var serviceIds = (await db.StfcTerritoryServices.Select(s => s.Id).ToListAsync()).ToHashSet();
        var serverIds = await db.GuildAlliances.Select(ga => ga.StfcAlliance.ServerId).Distinct().ToListAsync();

        var serversSynced = 0;
        var serversNotFound = 0;
        var slotsInserted = 0;
        var skippedUnknownTerritory = 0;
        var skippedUnknownService = 0;

        foreach (var serverId in serverIds)
        {
            var response = await client.GetAsync($"data/service_slots/service_slots_{serverId}.json");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                serversNotFound++;
                continue;
            }
            response.EnsureSuccessStatusCode();
            var slots = await response.Content.ReadFromJsonAsync<ServiceSlotsDto>();
            if (slots?.Territories is null)
            {
                serversNotFound++;
                continue;
            }

            // Replace-all-for-this-server so a zone dropping a service is reflected.
            var existingForServer = await db.StfcTerritoryServiceSlots.Where(s => s.ServerId == serverId).ToListAsync();
            db.StfcTerritoryServiceSlots.RemoveRange(existingForServer);

            foreach (var (tidStr, svids) in slots.Territories)
            {
                if (!int.TryParse(tidStr, out var tid) || !territoryIds.Contains(tid))
                {
                    skippedUnknownTerritory++;
                    continue;
                }

                var position = 0;
                foreach (var svid in svids)
                {
                    if (!serviceIds.Contains(svid))
                    {
                        skippedUnknownService++;
                        continue;
                    }
                    db.StfcTerritoryServiceSlots.Add(new StfcTerritoryServiceSlot
                    {
                        ServerId = serverId,
                        TerritoryId = tid,
                        ServiceId = svid,
                        Position = position++,
                    });
                    slotsInserted++;
                }
            }

            serversSynced++;
        }

        // 3. Record the meta so the next run can short-circuit.
        var syncedAt = DateTimeOffset.UtcNow;
        state ??= new TerritoryServiceSyncState();
        state.TcSeason = meta.TcSeason;
        state.GeneratedAt = meta.GeneratedAt;
        state.SyncedAt = syncedAt;
        if (state.Id == 0)
            db.TerritoryServiceSyncStates.Add(state);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Territory service sync (season {Season}): {Services} services, {Servers} servers, {Slots} slots.",
            meta.TcSeason, servicesUpserted, serversSynced, slotsInserted);

        return new TerritoryServiceSyncResult(false, meta.TcSeason, servicesUpserted, serversSynced,
            slotsInserted, serversNotFound, skippedUnknownTerritory, skippedUnknownService, syncedAt);
    }

    private record MetaDto(
        [property: JsonPropertyName("tcSeason")] string? TcSeason,
        [property: JsonPropertyName("generatedAt")] long GeneratedAt);

    private record StaticDto(
        [property: JsonPropertyName("territory_service_specs")] Dictionary<string, SpecDto> Specs);

    private record SpecDto(
        [property: JsonPropertyName("id_refs")] IdRefsDto? IdRefs,
        [property: JsonPropertyName("rarity")] int Rarity,
        [property: JsonPropertyName("name")] string? Name);

    private record IdRefsDto([property: JsonPropertyName("loca_id")] int? LocaId);

    private record TranslationRootDto(
        [property: JsonPropertyName("translations")] TranslationsDto Translations);

    private record TranslationsDto(
        [property: JsonPropertyName("territory_capture")] List<TransEntryDto> TerritoryCapture);

    private record TransEntryDto(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("text")] string Text);

    private record ServiceSlotsDto(
        [property: JsonPropertyName("territories")] Dictionary<string, List<long>> Territories);
}

public record TerritoryServiceSyncResult(
    bool Skipped,
    string? Season,
    int ServicesUpserted,
    int ServersSynced,
    int SlotsInserted,
    int ServersNotFound,
    int SkippedUnknownTerritory,
    int SkippedUnknownService,
    DateTimeOffset SyncedAt)
{
    public static TerritoryServiceSyncResult NoOp(string? season, DateTimeOffset syncedAt) =>
        new(true, season, 0, 0, 0, 0, 0, 0, syncedAt);
}
