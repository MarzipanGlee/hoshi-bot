using HoshiBot.Data;
using HoshiBot.Data.Seeding;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Services;

// One server record from the server-overview feed (see Manage/Stfc/ServerPages/Import.razor) —
// same shape as tools/HoshiBot.StfcSeedSync's StfcServerSeedData.json.
public record StfcCatalogServerEntry(int Number, string Name, string Region, string? VeilGroup);

// One Discord-invite record, joined on Number — same shape as StfcServerInviteSeedData.json.
public record StfcCatalogInviteEntry(int ServerId, string? DiscordUrl);

public record StfcCatalogImportResult(
    int RegionsAdded, int VeilGroupsAdded, int ServersAdded, int ServersUpdated, int InvitesAdded,
    int UnknownRegion, int UnknownVeilGroup);

// Upserts the Region/VeilGroup/Server catalog (+ Discord invites) from a fresh server-overview
// feed — the ongoing-refresh counterpart to SeedStfcCatalogIfEmptyAsync, which only ever runs
// once against an empty catalog. Region/VeilGroup names are assigned the same stable IDs via
// ScopelyCatalogIds, so a name already known to the seeder resolves to the same row here.
// Invites are additive-only (never edited/removed) — a server can legitimately have more than
// one active invite (see StfcServerDiscordInvite), so this only adds a row for a URL not
// already recorded for that server, and never touches existing ones.
public class StfcCatalogImportService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<StfcCatalogImportResult> ImportAsync(
        IReadOnlyList<StfcCatalogServerEntry> servers, IReadOnlyList<StfcCatalogInviteEntry> invites)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var regionsByName = await db.StfcRegions.ToDictionaryAsync(r => r.Name);
        var veilGroupsByName = await db.StfcVeilGroups.ToDictionaryAsync(v => v.Name);
        var serversById = await db.StfcServers.ToDictionaryAsync(s => s.Id);

        var regionsAdded = 0;
        var veilGroupsAdded = 0;
        var serversAdded = 0;
        var serversUpdated = 0;
        var unknownRegion = 0;
        var unknownVeilGroup = 0;

        foreach (var entry in servers)
        {
            if (!regionsByName.TryGetValue(entry.Region, out var region))
            {
                if (!ScopelyCatalogIds.Regions.TryGetValue(entry.Region, out var regionId))
                {
                    unknownRegion++;
                    continue;
                }

                region = new StfcRegion { Id = regionId, Name = entry.Region };
                db.StfcRegions.Add(region);
                regionsByName[entry.Region] = region;
                regionsAdded++;
            }

            StfcVeilGroup? veilGroup = null;
            if (entry.VeilGroup is not null)
            {
                if (!veilGroupsByName.TryGetValue(entry.VeilGroup, out veilGroup))
                {
                    if (!ScopelyCatalogIds.VeilGroups.TryGetValue(entry.VeilGroup, out var veilGroupId))
                    {
                        unknownVeilGroup++;
                        continue;
                    }

                    veilGroup = new StfcVeilGroup { Id = veilGroupId, Name = entry.VeilGroup, RegionId = region.Id };
                    db.StfcVeilGroups.Add(veilGroup);
                    veilGroupsByName[entry.VeilGroup] = veilGroup;
                    veilGroupsAdded++;
                }
            }

            if (serversById.TryGetValue(entry.Number, out var server))
            {
                if (server.Name != entry.Name || server.RegionId != region.Id || server.VeilGroupId != veilGroup?.Id)
                {
                    server.Name = entry.Name;
                    server.RegionId = region.Id;
                    server.VeilGroupId = veilGroup?.Id;
                    serversUpdated++;
                }
            }
            else
            {
                server = new StfcServer { Id = entry.Number, Name = entry.Name, RegionId = region.Id, VeilGroupId = veilGroup?.Id };
                db.StfcServers.Add(server);
                serversById[entry.Number] = server;
                serversAdded++;
            }
        }

        // Existing invites are loaded per-server on demand rather than up front — most imports
        // touch far fewer servers than the full catalog, and invites are a small table.
        var invitesAdded = 0;
        foreach (var group in invites.Where(i => !string.IsNullOrEmpty(i.DiscordUrl)).GroupBy(i => i.ServerId))
        {
            if (!serversById.ContainsKey(group.Key))
                continue;

            var existingUrls = await db.StfcServerDiscordInvites
                .Where(i => i.ServerId == group.Key)
                .Select(i => i.Url)
                .ToHashSetAsync();

            foreach (var invite in group)
            {
                if (existingUrls.Add(invite.DiscordUrl!))
                {
                    db.StfcServerDiscordInvites.Add(new StfcServerDiscordInvite { ServerId = group.Key, Url = invite.DiscordUrl! });
                    invitesAdded++;
                }
            }
        }

        await db.SaveChangesAsync();

        return new StfcCatalogImportResult(regionsAdded, veilGroupsAdded, serversAdded, serversUpdated, invitesAdded, unknownRegion, unknownVeilGroup);
    }
}
