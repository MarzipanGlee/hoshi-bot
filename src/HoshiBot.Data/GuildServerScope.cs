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
    // Two sources, unioned: the guild's linked alliances (and the servers those sit on), plus any
    // server the guild tracks in its own right.
    //
    // Linked veil groups are deliberately NOT a third source. A veil group holds ~20 servers, so
    // including them would make most of a region "ours" off the back of a link that is really about
    // veil features — a guild linking one is not saying it belongs to all 20. If a veil-group-wide
    // Discord ever wants a role per server in its group, that belongs behind its own opt-in rather
    // than silently widening what "foreign" means for nicknames and alliance tag roles.
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

        return new GuildScope(
            allianceIds.ToHashSet(),
            allianceServerIds.Concat(trackedServerIds).ToHashSet());
    }
}
