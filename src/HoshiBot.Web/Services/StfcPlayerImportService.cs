using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// One entry from an external player-data feed (see Manage/Stfc/PlayerPages/Import.razor for
// where these come from) — deliberately just the fields the upsert actually needs, not a
// 1:1 mirror of the feed's much larger per-player payload.
public record StfcPlayerImportEntry(long ExternalId, string? Name, string? AllianceTag, int Server, int RankId, int Level);

public record StfcPlayerImportResult(int Added, int Updated, int Renamed, int Mismatched, int InvalidRank, int MissingName);

// Upserts StfcPlayer rows from an external player-data feed — same shape as
// StfcSystemSyncService's sync (dictionary-preload by natural key, update-existing-or-insert,
// one SaveChangesAsync, summary counts), keyed by ExternalId instead of Number and scoped to
// one server at a time (the admin picks a server on the Import page; a feed page can only
// ever cover one server's roster).
public class StfcPlayerImportService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<StfcPlayerImportResult> ImportAsync(int serverId, IReadOnlyList<StfcPlayerImportEntry> entries)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existingByExternalId = await db.StfcPlayers
            .Where(p => p.ServerId == serverId)
            .ToDictionaryAsync(p => p.ExternalId);

        var alliancesByTag = await db.StfcAlliances
            .Where(a => a.ServerId == serverId)
            .ToDictionaryAsync(a => a.Tag);

        var seenAt = DateTimeOffset.UtcNow;
        var added = 0;
        var updated = 0;
        var renamed = 0;
        var mismatched = 0;
        var invalidRank = 0;
        var missingName = 0;

        foreach (var entry in entries)
        {
            if (entry.Server != serverId)
            {
                mismatched++;
                continue;
            }

            // The feed occasionally has a null/empty name for a player (seen in real data,
            // not just a theoretical edge case) — skip rather than let a NOT NULL violation
            // on StfcPlayerNameHistory abort the whole batch's SaveChangesAsync.
            if (string.IsNullOrEmpty(entry.Name))
            {
                missingName++;
                continue;
            }

            StfcPlayerRank? rank = null;
            if (Enum.IsDefined(typeof(StfcPlayerRank), entry.RankId))
                rank = (StfcPlayerRank)entry.RankId;
            else
                invalidRank++;

            var alliance = entry.AllianceTag is not null ? alliancesByTag.GetValueOrDefault(entry.AllianceTag) : null;

            if (existingByExternalId.TryGetValue(entry.ExternalId, out var existing))
            {
                if (existing.Name != entry.Name)
                {
                    existing.NameHistory.Add(new StfcPlayerNameHistory { Name = entry.Name, ObservedAt = seenAt });
                    existing.Name = entry.Name;
                    renamed++;
                }

                existing.AllianceId = alliance?.Id;
                if (rank is not null)
                    existing.Rank = rank;
                existing.OpsLevel = entry.Level;

                updated++;
            }
            else
            {
                var player = new StfcPlayer
                {
                    ExternalId = entry.ExternalId,
                    Name = entry.Name,
                    ServerId = serverId,
                    AllianceId = alliance?.Id,
                    Rank = rank,
                    OpsLevel = entry.Level,
                };
                player.NameHistory.Add(new StfcPlayerNameHistory { Name = entry.Name, ObservedAt = seenAt });

                db.StfcPlayers.Add(player);
                existingByExternalId[entry.ExternalId] = player;
                added++;
            }
        }

        await db.SaveChangesAsync();

        return new StfcPlayerImportResult(added, updated, renamed, mismatched, invalidRank, missingName);
    }
}
