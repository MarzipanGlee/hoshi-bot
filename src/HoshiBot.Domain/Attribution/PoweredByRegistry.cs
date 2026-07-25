using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Attribution;

// The static entity→source map behind the "Powered by" credits. Keyed by domain entity type so a
// page/feature just names the entities it displays or depends on, and the credit(s) follow from the
// data's actual origin (documented in each entity's header comment). Excluded on purpose: the
// admin-curated Discord-invite tables (StfcAllianceDiscordInvite/StfcVeilGroupDiscordInvite — no
// external seed), StfcAllianceDiplomacy (bot-set via /set-diplomacy), StfcClientRelease (app stores),
// StfcNewsPost/StfcEventDateConfirmation (Scopely RSS / bot-internal).
public static class PoweredByRegistry
{
    private static readonly Dictionary<Type, PoweredBySource> ByEntity = new()
    {
        // stfc.pro — alliance/player/server catalog (via tools/HoshiBot.StfcSeedSync) + territory
        // ownership feed + server/event status (api.stfc.pro).
        [typeof(StfcRegion)] = PoweredBySources.StfcPro,
        [typeof(StfcVeilGroup)] = PoweredBySources.StfcPro,
        [typeof(StfcServer)] = PoweredBySources.StfcPro,
        [typeof(StfcServerDiscordInvite)] = PoweredBySources.StfcPro,
        [typeof(StfcAlliance)] = PoweredBySources.StfcPro,
        [typeof(StfcAllianceNameHistory)] = PoweredBySources.StfcPro,
        [typeof(StfcPlayer)] = PoweredBySources.StfcPro,
        [typeof(StfcPlayerNameHistory)] = PoweredBySources.StfcPro,
        [typeof(StfcTerritoryOwnership)] = PoweredBySources.StfcPro,
        [typeof(StfcServerStatus)] = PoweredBySources.StfcPro,
        [typeof(StfcEventStatus)] = PoweredBySources.StfcPro,

        // territory.lol — Territory Capture territory catalog + services.
        [typeof(StfcTerritory)] = PoweredBySources.TerritoryLol,
        [typeof(StfcTerritoryNeighbour)] = PoweredBySources.TerritoryLol,
        [typeof(StfcTerritoryService)] = PoweredBySources.TerritoryLol,
        [typeof(StfcTerritoryServiceSlot)] = PoweredBySources.TerritoryLol,

        // stfc.space — the daily system sync (data.stfc.space).
        [typeof(StfcSystem)] = PoweredBySources.StfcSpace,
    };

    // The distinct sources behind the given entities, in first-seen order (so a feature reading
    // several stfc.pro entities shows one credit; Territory Capture's territory.lol + stfc.pro mix
    // shows two). Throws for an unregistered type — a wiring bug, caught at first render / by tests.
    public static IReadOnlyList<PoweredBySource> For(params Type[] entities)
    {
        var result = new List<PoweredBySource>();
        foreach (var entity in entities)
        {
            if (!ByEntity.TryGetValue(entity, out var source))
                throw new InvalidOperationException($"No 'Powered by' source registered for entity type '{entity.Name}'.");
            if (!result.Contains(source))
                result.Add(source);
        }
        return result;
    }

    // Exposed for tests: every entity type that has a registered source.
    public static IReadOnlyCollection<Type> RegisteredEntities => ByEntity.Keys;
}
