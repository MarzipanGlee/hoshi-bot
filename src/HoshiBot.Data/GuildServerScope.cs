using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Which alliances and servers a guild counts as "its own". Lives in Data rather than next to the
// jobs because the Server Tag Roles editor has to offer exactly the servers its sync job will act
// on — a second definition on the Web side would drift the moment either is touched.
//
// NicknameSyncJob and AllianceTagRoleSyncJob each carried a byte-identical copy of the alliance +
// server halves of this before it was extracted.
public record GuildScope(HashSet<int> AllianceIds, HashSet<int> ServerIds);

public static class GuildServerScope
{
    // Three sources, unioned: the guild's linked alliances (and the servers those sit on), any
    // server the guild tracks in its own right, and every server in a linked veil group.
    //
    // The veil-group arm is the broad one — a veil group holds ~20 servers, so a guild that links
    // one considers most of its region home. That is deliberate: linking a veil group is a
    // statement about which part of the game this Discord belongs to.
    //
    // StfcServer.VeilGroupId is nullable (a newly-launched server exists before players can fly to
    // a veil group area), so servers not assigned to one simply don't come in through that arm.
    public static async Task<GuildScope> ResolveAsync(HoshiBotDbContext db, ulong guildId, CancellationToken cancellationToken = default)
    {
        var allianceIds = await db.GuildAlliances
            .Where(ga => ga.GuildId == guildId)
            .Select(ga => ga.StfcAllianceId)
            .ToListAsync(cancellationToken);

        var allianceServerIds = await db.StfcAlliances
            .Where(a => allianceIds.Contains(a.Id))
            .Select(a => a.ServerId)
            .ToListAsync(cancellationToken);

        var trackedServerIds = await db.GuildServers
            .Where(gs => gs.GuildId == guildId)
            .Select(gs => gs.StfcServerId)
            .ToListAsync(cancellationToken);

        var veilGroupIds = await db.GuildVeilGroups
            .Where(gv => gv.GuildId == guildId)
            .Select(gv => gv.StfcVeilGroupId)
            .ToListAsync(cancellationToken);

        var veilGroupServerIds = await db.StfcServers
            .Where(s => s.VeilGroupId != null && veilGroupIds.Contains(s.VeilGroupId.Value))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        return new GuildScope(
            allianceIds.ToHashSet(),
            allianceServerIds.Concat(trackedServerIds).Concat(veilGroupServerIds).ToHashSet());
    }
}
