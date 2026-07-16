using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// One entry from an external alliance-data feed (see Manage/Stfc/AlliancePages/Import.razor for
// where these come from) — deliberately just the fields the upsert actually needs, not a 1:1
// mirror of the feed's much larger per-alliance payload (rank/power/slogan/…).
public record StfcAllianceImportEntry(long ExternalId, int ServerId, string Tag, string Name, int Emblem);

public record StfcAllianceImportResult(int Added, int Updated, int Renamed, int UnknownServer);

// Upserts StfcAlliance rows from an external alliance-data feed — same shape as
// StfcPlayerImportService (dictionary-preload by natural key, update-existing-or-insert, one
// SaveChangesAsync, summary counts), keyed by ExternalId. Unlike players, alliances aren't
// scoped to one server picked up front — the feed already spans every server, matching how
// tools/HoshiBot.StfcSeedSync processes the same raw dump.
public class StfcAllianceImportService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<StfcAllianceImportResult> ImportAsync(IReadOnlyList<StfcAllianceImportEntry> entries)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existingByExternalId = await db.StfcAlliances.ToDictionaryAsync(a => a.ExternalId);
        var knownServerIds = (await db.StfcServers.Select(s => s.Id).ToListAsync()).ToHashSet();

        var seenAt = DateTimeOffset.UtcNow;
        var added = 0;
        var updated = 0;
        var renamed = 0;
        var unknownServer = 0;

        foreach (var entry in entries)
        {
            if (!knownServerIds.Contains(entry.ServerId))
            {
                unknownServer++;
                continue;
            }

            if (existingByExternalId.TryGetValue(entry.ExternalId, out var existing))
            {
                // A NameHistory row is only added when Tag or Name actually changed from the
                // alliance's current values, not on every sync regardless — see
                // StfcAllianceNameHistory's doc comment.
                if (existing.Tag != entry.Tag || existing.Name != entry.Name)
                {
                    existing.NameHistory.Add(new StfcAllianceNameHistory { Tag = entry.Tag, Name = entry.Name, ObservedAt = seenAt });
                    existing.Tag = entry.Tag;
                    existing.Name = entry.Name;
                    renamed++;
                }

                existing.Emblem = entry.Emblem;
                updated++;
            }
            else
            {
                var alliance = new StfcAlliance
                {
                    ExternalId = entry.ExternalId,
                    Tag = entry.Tag,
                    Name = entry.Name,
                    ServerId = entry.ServerId,
                    Emblem = entry.Emblem,
                };
                alliance.NameHistory.Add(new StfcAllianceNameHistory { Tag = entry.Tag, Name = entry.Name, ObservedAt = seenAt });

                db.StfcAlliances.Add(alliance);
                existingByExternalId[entry.ExternalId] = alliance;
                added++;
            }
        }

        await db.SaveChangesAsync();

        return new StfcAllianceImportResult(added, updated, renamed, unknownServer);
    }
}
