using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// One entry from an external server-status feed (see Manage/Stfc/ServerStatusPages/Import.razor
// for where these come from) — same shape as StfcServerStatusSeedData's raw JSON.
public record StfcServerStatusImportEntry(int StfcServerId, int Status, string Maintenance);

public record StfcServerStatusImportResult(int Added, int Updated, int UnknownServer);

// Upserts StfcServerStatus rows from a fresh server-status snapshot — the manual-refresh
// counterpart to SeedStfcServerStatusIfEmptyAsync (which only ever seeds an empty table once):
// there is no live sync today (stfc.pro's /api/ is not robots.txt-permitted, see
// docs/stfc-api-requirements.md), so this is the only way to bring the table current without a
// reseed. For an EXISTING row, Notified* is deliberately left untouched — that's what
// ServerStatusNotifyJob compares Status/Maintenance against to decide whether to announce a
// change, and letting a fresh snapshot update Status/Maintenance without also updating Notified*
// is exactly what lets the job detect and announce the change. For a brand-new row (first status
// ever seen for a server), Notified* is set equal to the observed values instead, mirroring the
// seeder — otherwise the job would fire an unwanted "just changed" notification for a server it's
// never seen before.
public class StfcServerStatusImportService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<StfcServerStatusImportResult> ImportAsync(IReadOnlyList<StfcServerStatusImportEntry> entries)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var knownServerIds = (await db.StfcServers.Select(s => s.Id).ToListAsync()).ToHashSet();
        var existingByServerId = await db.StfcServerStatuses.ToDictionaryAsync(s => s.StfcServerId);

        var seenAt = DateTimeOffset.UtcNow;
        var added = 0;
        var updated = 0;
        var unknownServer = 0;

        foreach (var entry in entries)
        {
            if (!knownServerIds.Contains(entry.StfcServerId))
            {
                unknownServer++;
                continue;
            }

            if (existingByServerId.TryGetValue(entry.StfcServerId, out var existing))
            {
                existing.Status = entry.Status;
                existing.Maintenance = entry.Maintenance;
                existing.UpdatedAt = seenAt;
                updated++;
            }
            else
            {
                db.StfcServerStatuses.Add(new StfcServerStatus
                {
                    StfcServerId = entry.StfcServerId,
                    Status = entry.Status,
                    Maintenance = entry.Maintenance,
                    UpdatedAt = seenAt,
                    NotifiedStatus = entry.Status,
                    NotifiedMaintenance = entry.Maintenance,
                });
                added++;
            }
        }

        await db.SaveChangesAsync();

        return new StfcServerStatusImportResult(added, updated, unknownServer);
    }
}
