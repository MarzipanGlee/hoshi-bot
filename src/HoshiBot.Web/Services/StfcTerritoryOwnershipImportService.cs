using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// One entry from stfc.pro's territory-ownership feed (see
// Manage/Stfc/TerritoryOwnershipPages/Import.razor) — same shape as
// StfcTerritoryOwnershipSeedData's raw JSON. Tag is null for an unowned territory.
public record StfcTerritoryOwnershipImportEntry(int Server, int Territory, string? Tag);

public record StfcTerritoryOwnershipImportResult(int Added, int Updated, int Removed, int UnknownTerritory, int UnresolvedTag);

// Upserts StfcTerritoryOwnership rows from a fresh ownership snapshot — the ongoing-refresh
// counterpart to SeedStfcTerritoryOwnershipIfEmptyAsync (which only ever seeds an empty table
// once). Unlike the seeder, this runs against a table that may already hold real ownership —
// so unlike a plain insert-only seed, a null Tag here means the territory is now unowned and any
// existing row for it is removed (there's no nullable AllianceId column; "no row" *is* how
// unowned is represented). A Tag that can't be resolved to a known alliance on that server is
// left untouched rather than guessed at.
public class StfcTerritoryOwnershipImportService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<StfcTerritoryOwnershipImportResult> ImportAsync(IReadOnlyList<StfcTerritoryOwnershipImportEntry> entries)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var territoryIds = (await db.StfcTerritories.Select(t => t.Id).ToListAsync()).ToHashSet();

        var alliances = await db.StfcAlliances.Select(a => new { a.Id, a.ServerId, a.Tag }).ToListAsync();
        var allianceIdByServerTag = alliances
            .GroupBy(a => (a.ServerId, a.Tag))
            .ToDictionary(g => g.Key, g => g.First().Id);

        var existingByKey = await db.StfcTerritoryOwnerships
            .ToDictionaryAsync(o => (o.ServerId, o.TerritoryId));

        var added = 0;
        var updated = 0;
        var removed = 0;
        var unknownTerritory = 0;
        var unresolvedTag = 0;

        foreach (var entry in entries)
        {
            if (!territoryIds.Contains(entry.Territory))
            {
                unknownTerritory++;
                continue;
            }

            var key = (entry.Server, entry.Territory);
            existingByKey.TryGetValue(key, out var existing);

            if (entry.Tag is null)
            {
                if (existing is not null)
                {
                    db.StfcTerritoryOwnerships.Remove(existing);
                    existingByKey.Remove(key);
                    removed++;
                }
                continue;
            }

            if (!allianceIdByServerTag.TryGetValue((entry.Server, entry.Tag), out var allianceId))
            {
                unresolvedTag++;
                continue;
            }

            if (existing is not null)
            {
                if (existing.AllianceId != allianceId)
                {
                    existing.AllianceId = allianceId;
                    updated++;
                }
            }
            else
            {
                var ownership = new StfcTerritoryOwnership { TerritoryId = entry.Territory, ServerId = entry.Server, AllianceId = allianceId };
                db.StfcTerritoryOwnerships.Add(ownership);
                existingByKey[key] = ownership;
                added++;
            }
        }

        await db.SaveChangesAsync();

        return new StfcTerritoryOwnershipImportResult(added, updated, removed, unknownTerritory, unresolvedTag);
    }
}
