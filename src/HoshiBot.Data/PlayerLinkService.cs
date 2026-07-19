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

    // Every linked player for each of the given users (for the assignment page), main first.
    public async Task<Dictionary<ulong, List<MemberPlayerLink>>> GetLinksForUsersAsync(IEnumerable<ulong> userIds)
    {
        var ids = userIds.ToList();
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.UserPlayers
            .Where(up => ids.Contains(up.DiscordUserId))
            .Select(up => new
            {
                up.DiscordUserId,
                up.IsMain,
                PlayerId = up.StfcPlayerId,
                up.StfcPlayer.Name,
                ServerName = up.StfcPlayer.Server.Name,
                AllianceTag = up.StfcPlayer.Alliance != null ? up.StfcPlayer.Alliance.Tag : null,
            })
            .ToListAsync();

        return rows
            .GroupBy(r => r.DiscordUserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new MemberPlayerLink(r.PlayerId, r.Name, r.ServerName, r.AllianceTag, r.IsMain))
                    .OrderByDescending(l => l.IsMain)
                    .ThenBy(l => l.Name)
                    .ToList());
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
        if (await db.GuildMembers.FindAsync(guildId, userId) is null)
            db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = userId, JoinedAt = DateTimeOffset.UtcNow });
    }

    // Idempotently link a Discord user to an StfcPlayer. Creates the DiscordUser row if missing and
    // marks the link IsMain when it's the user's first — mirrors PlayerModule's /link-player logic.
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

        var hasAnyLink = await db.UserPlayers.AnyAsync(up => up.DiscordUserId == userId);
        db.UserPlayers.Add(new UserPlayer
        {
            DiscordUserId = userId,
            StfcPlayerId = stfcPlayerId,
            IsMain = !hasAnyLink,
        });
    }

    // Make one of a user's linked players their main (unsets the others). No-op if not linked.
    public async Task SetMainAsync(ulong userId, int stfcPlayerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var links = await db.UserPlayers.Where(up => up.DiscordUserId == userId).ToListAsync();
        if (links.All(up => up.StfcPlayerId != stfcPlayerId))
            return;

        foreach (var link in links)
            link.IsMain = link.StfcPlayerId == stfcPlayerId;
        await db.SaveChangesAsync();
    }

    // Remove one link; if it was the main and other links remain, promote the oldest to main so a user
    // is never left with links but no main.
    public async Task UnlinkAsync(ulong userId, int stfcPlayerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var link = await db.UserPlayers.FirstOrDefaultAsync(up => up.DiscordUserId == userId && up.StfcPlayerId == stfcPlayerId);
        if (link is null)
            return;

        var wasMain = link.IsMain;
        db.UserPlayers.Remove(link);
        await db.SaveChangesAsync();

        if (!wasMain)
            return;

        var next = await db.UserPlayers.Where(up => up.DiscordUserId == userId).OrderBy(up => up.Id).FirstOrDefaultAsync();
        if (next is not null)
        {
            next.IsMain = true;
            await db.SaveChangesAsync();
        }
    }

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
}

public enum PlayerLinkOutcome
{
    Linked,          // confident match → silently linked
    Queued,          // ambiguous/no match → Unresolved review row
    AlreadyLinked,   // member already has a UserPlayer link
    AlreadyResolved, // member already has a terminal review
}

public record PlayerSearchResult(int Id, string Name, string ServerName, string? AllianceTag);

public record MemberPlayerLink(int PlayerId, string Name, string ServerName, string? AllianceTag, bool IsMain);
