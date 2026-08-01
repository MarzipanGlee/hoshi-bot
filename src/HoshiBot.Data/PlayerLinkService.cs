using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// The pure-DB core of player↔member assignment. Player links live on the user globally
// (DiscordUser.PlayerLinks / UserPlayer), so a person's players are known in every guild Hoshi shares
// with them — this service creates/edits those links, drives the guild-wide auto-matcher, backs the
// full-catalog search + admin assignment page, and maintains the PlayerLinkReview onboarding queue.
// Lives in HoshiBot.Data (no NetCord/Quartz) so the Discord jobs/handlers AND the Web page can all
// call it. Discord-layer callers pass a tag-stripped nickname (CommanderName.Of); no Discord I/O here.
public class PlayerLinkService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    // Global exact-name matches for a display name, ordered with the guild's linked-alliance players
    // first. Used by the auto-matcher and the onboarding DM's candidate resolution.
    public async Task<List<StfcPlayer>> ResolveCandidatesAsync(ulong guildId, string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var allianceIds = await GuildStfcAllianceIdsAsync(db, guildId);
        return await ResolveCandidatesAsync(db, allianceIds, name);
    }

    private static async Task<List<StfcPlayer>> ResolveCandidatesAsync(HoshiBotDbContext db, HashSet<int> guildStfcAllianceIds, string name)
    {
        var lowered = name.Trim().ToLower();
        if (lowered.Length == 0)
            return [];

        var matches = await db.StfcPlayers
            .Where(p => p.Name.ToLower() == lowered)
            .ToListAsync();

        return matches
            .OrderByDescending(p => p.AllianceId != null && guildStfcAllianceIds.Contains(p.AllianceId.Value))
            .ThenBy(p => p.Name)
            .ToList();
    }

    // Search-as-you-type over the whole player catalog for the assignment page's picker — capped, and
    // it never excludes a player already linked to another user (multi-account owners are legitimate).
    public async Task<List<PlayerSearchResult>> SearchPlayersAsync(string term, int limit = 25)
    {
        var t = term.Trim().ToLower();
        if (t.Length == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.StfcPlayers
            .Where(p => p.Name.ToLower().Contains(t))
            .OrderBy(p => p.Name)
            .Take(limit)
            .Select(p => new PlayerSearchResult(p.Id, p.Name, p.Server.Name, p.Alliance != null ? p.Alliance.Tag : null))
            .ToListAsync();
    }

    // Every linked player for each of the given users (for the assignment page and /me). With a
    // guildId, each link also carries whether it's that guild's primary — the pick that drives
    // nickname/role sync there; without one, IsGuildPrimary is always false (there's no guild to
    // be primary in).
    public async Task<Dictionary<ulong, List<MemberPlayerLink>>> GetLinksForUsersAsync(IEnumerable<ulong> userIds, ulong? guildId = null)
    {
        var ids = userIds.ToList();
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.UserPlayers
            .Where(up => ids.Contains(up.DiscordUserId))
            .Select(up => new
            {
                up.DiscordUserId,
                PlayerId = up.StfcPlayerId,
                up.StfcPlayer.Name,
                ServerName = up.StfcPlayer.Server.Name,
                AllianceTag = up.StfcPlayer.Alliance != null ? up.StfcPlayer.Alliance.Tag : null,
            })
            .ToListAsync();

        var primaryByUser = guildId is { } gid
            ? await GuildPrimaryPlayerIdsAsync(db, gid, ids)
            : [];

        return rows
            .GroupBy(r => r.DiscordUserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new MemberPlayerLink(r.PlayerId, r.Name, r.ServerName, r.AllianceTag,
                        primaryByUser.TryGetValue(r.DiscordUserId, out var primaryId) && primaryId == r.PlayerId))
                    .OrderByDescending(l => l.IsGuildPrimary)
                    .ThenBy(l => l.Name)
                    .ToList());
    }

    // The player each of a guild's members is represented by: their per-guild pick, falling back to
    // their oldest link so a member who never chose (or whose pick was missed by a write path) still
    // syncs. The one place the sync jobs and identity lookups resolve "who is this member in-game".
    public async Task<Dictionary<ulong, GuildPrimaryPlayer>> GetGuildPrimaryPlayersAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var playerIdByUser = await GuildPrimaryPlayerIdsAsync(db, guildId, null);
        if (playerIdByUser.Count == 0)
            return [];

        var playerIds = playerIdByUser.Values.Distinct().ToList();
        var players = (await db.StfcPlayers
            .Where(p => playerIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.AllianceId,
                AllianceTag = p.Alliance != null ? p.Alliance.Tag : null,
                p.ServerId,
                RegionName = p.Server.Region.Name,
                p.Rank,
                p.OpsLevel,
            })
            .ToListAsync())
            .ToDictionary(p => p.Id);

        // The member's own nickname suffix (global, set on /me) — only Nickname Sync uses it, but it
        // rides along here so the job keeps its single roster read instead of a query per member.
        var userIdList = playerIdByUser.Keys.ToList();
        var suffixByUser = await db.DiscordUsers
            .Where(u => userIdList.Contains(u.DiscordUserId) && u.NicknameSuffix != null)
            .ToDictionaryAsync(u => u.DiscordUserId, u => u.NicknameSuffix!);

        var result = new Dictionary<ulong, GuildPrimaryPlayer>();
        foreach (var (userId, playerId) in playerIdByUser)
        {
            if (!players.TryGetValue(playerId, out var p))
                continue;
            result[userId] = new GuildPrimaryPlayer(userId, p.Id, p.Name, p.AllianceId, p.AllianceTag, p.ServerId, p.RegionName, p.Rank, p.OpsLevel,
                suffixByUser.GetValueOrDefault(userId));
        }
        return result;
    }

    // Single-member form of the above, for the Discord-side callers that resolve one member at a time.
    public async Task<int?> GetGuildPrimaryPlayerIdAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var map = await GuildPrimaryPlayerIdsAsync(db, guildId, [userId]);
        return map.TryGetValue(userId, out var playerId) ? playerId : null;
    }

    // pick ?? oldest link, for a guild's whole roster (userIds null) or just the given members.
    private static async Task<Dictionary<ulong, int>> GuildPrimaryPlayerIdsAsync(HoshiBotDbContext db, ulong guildId, List<ulong>? userIds)
    {
        var rows = await db.GuildMembers
            .Where(gm => gm.GuildId == guildId && (userIds == null || userIds.Contains(gm.DiscordUserId)))
            .Select(gm => new
            {
                gm.DiscordUserId,
                gm.PrimaryStfcPlayerId,
                // Same shape RankRoleSyncJob already proved translatable — a correlated subquery
                // projected to a nullable scalar.
                OldestLinkedPlayerId = gm.User.PlayerLinks.OrderBy(up => up.Id).Select(up => (int?)up.StfcPlayerId).FirstOrDefault(),
            })
            .ToListAsync();

        var result = new Dictionary<ulong, int>();
        foreach (var row in rows)
        {
            if ((row.PrimaryStfcPlayerId ?? row.OldestLinkedPlayerId) is { } playerId)
                result[row.DiscordUserId] = playerId;
        }
        return result;
    }

    // Set (or change) which linked player represents this member in this guild. Rejects a player the
    // person's account group isn't linked to. Returns false if it was rejected.
    public async Task<bool> SetGuildPrimaryAsync(ulong guildId, ulong userId, int stfcPlayerId)
    {
        var group = (await GetAccountGroupAsync(userId)).ToList();

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.UserPlayers.AnyAsync(up => group.Contains(up.DiscordUserId) && up.StfcPlayerId == stfcPlayerId))
            return false;

        await EnsureGuildMemberAsync(db, guildId, userId);
        await db.SaveChangesAsync();

        var member = await db.GuildMembers.FindAsync(guildId, userId);
        if (member is null)
            return false;

        member.PrimaryStfcPlayerId = stfcPlayerId;
        await db.SaveChangesAsync();
        return true;
    }

    // Records that a user is a member of a guild (DiscordUser + GuildMember). Every role-sync job
    // (rank/ops/nickname/…) iterates GuildMembers, so a link with no GuildMember row is invisible to
    // them — assignment paths must call this so assigned members actually get their roles.
    public async Task EnsureGuildMemberAsync(ulong guildId, ulong userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsureGuildMemberAsync(db, guildId, userId);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureGuildMemberAsync(HoshiBotDbContext db, ulong guildId, ulong userId)
    {
        if (await db.DiscordUsers.FindAsync(userId) is null)
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });
        if (await db.GuildMembers.FindAsync(guildId, userId) is not null)
            return;

        // Seed the guild's primary from what the person already plays, so a member who joins with
        // links in place syncs immediately instead of waiting for a pick they'd never know to make.
        var oldestLink = await OldestLinkedPlayerIdAsync(db, userId);
        db.GuildMembers.Add(new GuildMember
        {
            GuildId = guildId,
            DiscordUserId = userId,
            JoinedAt = DateTimeOffset.UtcNow,
            PrimaryStfcPlayerId = oldestLink,
        });
    }

    // Idempotently link a Discord user to an StfcPlayer. Creates the DiscordUser row if missing and
    // fills in any guild that has no primary player picked yet — mirrors /link-player's logic.
    public async Task LinkAsync(ulong userId, int stfcPlayerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await LinkAsync(db, userId, stfcPlayerId);
        await db.SaveChangesAsync();
    }

    private static async Task LinkAsync(HoshiBotDbContext db, ulong userId, int stfcPlayerId)
    {
        if (await db.DiscordUsers.FindAsync(userId) is null)
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });

        var alreadyLinked = await db.UserPlayers
            .AnyAsync(up => up.DiscordUserId == userId && up.StfcPlayerId == stfcPlayerId);
        if (alreadyLinked)
            return;

        db.UserPlayers.Add(new UserPlayer
        {
            DiscordUserId = userId,
            StfcPlayerId = stfcPlayerId,
        });

        // Every guild that has no pick yet adopts this player. Guilds where the member already chose
        // are left alone — that choice is theirs, not something a new link should overwrite.
        var unassigned = await db.GuildMembers
            .Where(gm => gm.DiscordUserId == userId && gm.PrimaryStfcPlayerId == null)
            .ToListAsync();
        foreach (var member in unassigned)
            member.PrimaryStfcPlayerId = stfcPlayerId;
    }

    // Remove one link, and repoint any guild that was using that player at the member's oldest
    // remaining link (null when none is left) so no guild points at a player they no longer play.
    public async Task UnlinkAsync(ulong userId, int stfcPlayerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var link = await db.UserPlayers.FirstOrDefaultAsync(up => up.DiscordUserId == userId && up.StfcPlayerId == stfcPlayerId);
        if (link is null)
            return;

        db.UserPlayers.Remove(link);
        await db.SaveChangesAsync();

        var affected = await db.GuildMembers
            .Where(gm => gm.DiscordUserId == userId && gm.PrimaryStfcPlayerId == stfcPlayerId)
            .ToListAsync();
        if (affected.Count == 0)
            return;

        var fallback = await OldestLinkedPlayerIdAsync(db, userId);
        foreach (var member in affected)
            member.PrimaryStfcPlayerId = fallback;
        await db.SaveChangesAsync();
    }

    private static Task<int?> OldestLinkedPlayerIdAsync(HoshiBotDbContext db, ulong userId) =>
        db.UserPlayers
            .Where(up => up.DiscordUserId == userId)
            .OrderBy(up => up.Id)
            .Select(up => (int?)up.StfcPlayerId)
            .FirstOrDefaultAsync();

    // Flip all of a user's non-terminal review rows (across guilds) to Resolved — "has a link" is a
    // global fact, so once linked no guild should still flag or DM them.
    public async Task MarkUserResolvedAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var reviews = await db.PlayerLinkReviews
            .Where(r => r.DiscordUserId == userId
                && r.Status != PlayerLinkReviewStatus.Resolved
                && r.Status != PlayerLinkReviewStatus.Ignored)
            .ToListAsync();
        if (reviews.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var review in reviews)
        {
            review.Status = PlayerLinkReviewStatus.Resolved;
            review.UpdatedAt = now;
        }
        await db.SaveChangesAsync();
    }

    // The auto-matcher run by the on-join/update handler and the backfill job for one member. Links
    // silently on a globally-unique nickname match OR a unique match within the guild's alliances;
    // anything else becomes an Unresolved PlayerLinkReview for onboarding. No-op if already linked or
    // terminally reviewed. Returns the outcome for logging/onboarding.
    public async Task<PlayerLinkOutcome> ProcessMemberAsync(ulong guildId, ulong userId, string nickname)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Assert guild membership up front (before any early return) so the role-sync jobs can see
        // this member — the backfill job walks every member, so this also heals links made before
        // this was added (e.g. manual assignments that never created a GuildMember row).
        await EnsureGuildMemberAsync(db, guildId, userId);
        await db.SaveChangesAsync();

        if (await db.UserPlayers.AnyAsync(up => up.DiscordUserId == userId))
            return PlayerLinkOutcome.AlreadyLinked;

        var review = await db.PlayerLinkReviews
            .FirstOrDefaultAsync(r => r.GuildId == guildId && r.DiscordUserId == userId);
        if (review is { Status: PlayerLinkReviewStatus.Resolved or PlayerLinkReviewStatus.Ignored or PlayerLinkReviewStatus.Declined })
            return PlayerLinkOutcome.AlreadyResolved;

        var allianceIds = await GuildStfcAllianceIdsAsync(db, guildId);
        var candidates = await ResolveCandidatesAsync(db, allianceIds, nickname);
        var inAlliance = candidates.Where(p => p.AllianceId != null && allianceIds.Contains(p.AllianceId.Value)).ToList();

        var confident = candidates.Count == 1 ? candidates[0] : inAlliance.Count == 1 ? inAlliance[0] : null;
        var now = DateTimeOffset.UtcNow;

        if (confident is not null)
        {
            await LinkAsync(db, userId, confident.Id);
            if (review is not null)
            {
                review.Status = PlayerLinkReviewStatus.Resolved;
                review.UpdatedAt = now;
            }
            await db.SaveChangesAsync();
            return PlayerLinkOutcome.Linked;
        }

        // Best-guess for the onboarding DM: prefer a single in-alliance hit, else a single global hit.
        var candidateId = inAlliance.FirstOrDefault()?.Id ?? candidates.FirstOrDefault()?.Id;
        if (review is null)
        {
            db.PlayerLinkReviews.Add(new PlayerLinkReview
            {
                GuildId = guildId,
                GuildAllianceId = null,
                DiscordUserId = userId,
                Nickname = nickname,
                Status = PlayerLinkReviewStatus.Unresolved,
                CandidateStfcPlayerId = candidateId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            review.Nickname = nickname;
            review.CandidateStfcPlayerId = candidateId;
            review.UpdatedAt = now;
        }
        await db.SaveChangesAsync();
        return PlayerLinkOutcome.Queued;
    }

    private static async Task<HashSet<int>> GuildStfcAllianceIdsAsync(HoshiBotDbContext db, ulong guildId) =>
        (await db.GuildAlliances.Where(ga => ga.GuildId == guildId).Select(ga => ga.StfcAllianceId).ToListAsync()).ToHashSet();

    // --- The person behind the accounts -------------------------------------------------------
    // Two Discord accounts are the same person when they share a linked player — that's the only
    // "these are both me" fact the schema has ever carried, and the /me account-linking flow makes
    // it explicit by giving both accounts the union of their players. The relation is transitive
    // (A–P1–B, B–P2–C ⇒ one person), so a group is a connected component of the account↔player graph.

    // Every Discord account that belongs to the same person as this one, including itself.
    public async Task<HashSet<ulong>> GetAccountGroupAsync(ulong userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var groups = await GetAccountGroupsAsync(db, [userId]);
        return groups[userId];
    }

    // Batch form, for callers resolving a whole roster at once (MemberNoteService's person keys).
    // Static so a caller holding its own DbContext can reuse it instead of opening a second one.
    public static async Task<Dictionary<ulong, HashSet<ulong>>> GetAccountGroupsAsync(
        HoshiBotDbContext db, IReadOnlyCollection<ulong> userIds, CancellationToken cancellationToken = default)
    {
        // Walk out from the requested accounts, collecting the account↔player edges of everything
        // reachable. Bounded: real chains are 1-2 hops, the cap only stops a pathological graph.
        var edges = new List<(ulong UserId, int PlayerId)>();
        var seenUsers = new HashSet<ulong>(userIds);
        var seenPlayers = new HashSet<int>();
        var userFrontier = userIds.ToList();

        for (var hop = 0; hop < 8 && userFrontier.Count > 0; hop++)
        {
            var fromUsers = await db.UserPlayers
                .Where(up => userFrontier.Contains(up.DiscordUserId))
                .Select(up => new { up.DiscordUserId, up.StfcPlayerId })
                .ToListAsync(cancellationToken);
            edges.AddRange(fromUsers.Select(e => (e.DiscordUserId, e.StfcPlayerId)));

            var playerFrontier = fromUsers.Select(e => e.StfcPlayerId).Where(seenPlayers.Add).ToList();
            if (playerFrontier.Count == 0)
                break;

            var fromPlayers = await db.UserPlayers
                .Where(up => playerFrontier.Contains(up.StfcPlayerId))
                .Select(up => new { up.DiscordUserId, up.StfcPlayerId })
                .ToListAsync(cancellationToken);
            edges.AddRange(fromPlayers.Select(e => (e.DiscordUserId, e.StfcPlayerId)));

            userFrontier = fromPlayers.Select(e => e.DiscordUserId).Where(seenUsers.Add).ToList();
        }

        // Connected components over those edges: accounts keyed "u{id}", players "p{id}" so the two
        // id spaces can share one union-find.
        var parent = new Dictionary<string, string>();
        string Find(string x)
        {
            parent.TryAdd(x, x);
            while (parent[x] != x)
                x = parent[x] = parent[parent[x]];
            return x;
        }
        void Union(string a, string b)
        {
            var (ra, rb) = (Find(a), Find(b));
            if (ra != rb)
                parent[ra] = rb;
        }

        foreach (var (user, player) in edges)
            Union($"u{user}", $"p{player}");

        var accountsByRoot = new Dictionary<string, HashSet<ulong>>();
        foreach (var user in edges.Select(e => e.UserId).Distinct())
        {
            var root = Find($"u{user}");
            if (!accountsByRoot.TryGetValue(root, out var members))
                accountsByRoot[root] = members = [];
            members.Add(user);
        }

        return userIds.Distinct().ToDictionary(
            id => id,
            id => parent.ContainsKey($"u{id}") && accountsByRoot.TryGetValue(Find($"u{id}"), out var group)
                ? [.. group]
                : new HashSet<ulong> { id });   // no links at all — a person of one account
    }

    // Which Discord accounts claim this player. Drives /me's claim check: a player already held by an
    // account outside your group can't be claimed, since claiming it would silently merge two people.
    public async Task<List<ulong>> GetPlayerOwnersAsync(int stfcPlayerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.UserPlayers
            .Where(up => up.StfcPlayerId == stfcPlayerId)
            .Select(up => up.DiscordUserId)
            .ToListAsync();
    }

    // What /me's "link another Discord account" writes once the OAuth round-trip proved the second
    // account is the same person's: give both accounts the union of their players, which is exactly
    // what makes them one person everywhere identity is derived from links.
    public async Task<AccountMergeOutcome> MergeAccountsAsync(ulong userId, ulong otherUserId)
    {
        if (userId == otherUserId)
            return AccountMergeOutcome.SameAccount;

        await using var db = await dbFactory.CreateDbContextAsync();
        var group = (await GetAccountGroupsAsync(db, [userId]))[userId];
        if (group.Contains(otherUserId))
            return AccountMergeOutcome.AlreadyLinked;

        var players = await db.UserPlayers
            .Where(up => up.DiscordUserId == userId || up.DiscordUserId == otherUserId)
            .Select(up => up.StfcPlayerId)
            .Distinct()
            .ToListAsync();
        // Nothing to link them *by*: the relation is "shares a player", so two account with no
        // players between them can't be recorded as one person. The caller tells them to add a
        // player account first.
        if (players.Count == 0)
            return AccountMergeOutcome.NoPlayers;

        foreach (var playerId in players)
        {
            await LinkAsync(db, userId, playerId);
            await LinkAsync(db, otherUserId, playerId);
        }
        await db.SaveChangesAsync();
        return AccountMergeOutcome.Linked;
    }
}

public enum AccountMergeOutcome
{
    Linked,        // the two accounts now share every player either of them had
    AlreadyLinked, // already the same person (they share a player already)
    SameAccount,   // approved the OAuth prompt with the account already signed in
    NoPlayers,     // neither account has a player link, so there's nothing to link them by
}

public enum PlayerLinkOutcome
{
    Linked,          // confident match → silently linked
    Queued,          // ambiguous/no match → Unresolved review row
    AlreadyLinked,   // member already has a UserPlayer link
    AlreadyResolved, // member already has a terminal review
}

public record PlayerSearchResult(int Id, string Name, string ServerName, string? AllianceTag);

public record MemberPlayerLink(int PlayerId, string Name, string ServerName, string? AllianceTag, bool IsGuildPrimary);

// The resolved "who this member is in-game, in this guild" — a superset of what the nickname, rank,
// ops-level and notification-role sync jobs each need, so one query serves all four.
public record GuildPrimaryPlayer(
    ulong DiscordUserId,
    int PlayerId,
    string Name,
    int? AllianceId,
    string? AllianceTag,
    int ServerId,
    string RegionName,
    StfcPlayerRank? Rank,
    int? OpsLevel,
    // The person's own alias from /me (DiscordUser.NicknameSuffix). Trailing + optional so the
    // consumers that don't care about it construct the record unchanged.
    string? NicknameSuffix = null);
