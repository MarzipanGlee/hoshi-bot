using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// The pure-DB core of automated player↔member assignment (the PlayerLink / MemberOnboarding
// features): resolves an StfcPlayer candidate set from a member's display name, creates the
// UserPlayer link that drives every role-sync job, and maintains the PlayerLinkReview admin queue.
// Lives in HoshiBot.Data (no NetCord/Quartz) so the Discord jobs/handlers AND the Web admin table can
// all call it — Web must not reference HoshiBot.Discord. Discord-layer callers pass a tag-stripped
// nickname (CommanderName.Of); this service does no Discord I/O of its own.
public class PlayerLinkService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    // Player candidates for a display name within one linked alliance's server, in-alliance first.
    public async Task<List<StfcPlayer>> ResolveCandidatesAsync(int guildAllianceId, string playerName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await ResolveCandidatesAsync(db, guildAllianceId, playerName);
    }

    private static async Task<List<StfcPlayer>> ResolveCandidatesAsync(HoshiBotDbContext db, int guildAllianceId, string playerName)
    {
        var link = await db.GuildAlliances
            .Include(ga => ga.StfcAlliance)
            .FirstOrDefaultAsync(ga => ga.Id == guildAllianceId);
        if (link is null)
            return [];

        var lowered = playerName.Trim().ToLower();
        if (lowered.Length == 0)
            return [];

        var serverId = link.StfcAlliance.ServerId;
        var stfcAllianceId = link.StfcAllianceId;
        var matches = await db.StfcPlayers
            .Where(p => p.ServerId == serverId && p.Name.ToLower() == lowered)
            .ToListAsync();

        // In-alliance players first (the confident case); same-name players elsewhere on the server after.
        return matches
            .OrderByDescending(p => p.AllianceId == stfcAllianceId)
            .ToList();
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

    // The matcher run by the on-join handler and the backfill job for one member. A confident single
    // in-alliance match → link silently (no review, no message). Anything else → upsert an Unresolved
    // review row for the admin table. No-op if the member is already linked or already has a
    // terminal review (Resolved/Ignored/Declined). Returns the outcome for logging/onboarding.
    public async Task<PlayerLinkOutcome> ProcessMemberAsync(ulong guildId, int guildAllianceId, ulong userId, string nickname)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.UserPlayers.AnyAsync(up => up.DiscordUserId == userId))
            return PlayerLinkOutcome.AlreadyLinked;

        var review = await db.PlayerLinkReviews
            .FirstOrDefaultAsync(r => r.GuildId == guildId && r.DiscordUserId == userId);
        if (review is { Status: PlayerLinkReviewStatus.Resolved or PlayerLinkReviewStatus.Ignored or PlayerLinkReviewStatus.Declined })
            return PlayerLinkOutcome.AlreadyResolved;

        var link = await db.GuildAlliances
            .Include(ga => ga.StfcAlliance)
            .FirstOrDefaultAsync(ga => ga.Id == guildAllianceId);
        if (link is null)
            return PlayerLinkOutcome.AlreadyResolved; // misconfigured link; nothing to match against

        var candidates = await ResolveCandidatesAsync(db, guildAllianceId, nickname);
        var inAlliance = candidates.Where(p => p.AllianceId == link.StfcAllianceId).ToList();

        var now = DateTimeOffset.UtcNow;

        // Confident: exactly one in-alliance roster match on the nickname → link silently.
        if (inAlliance.Count == 1)
        {
            await LinkAsync(db, userId, inAlliance[0].Id);
            if (review is not null)
            {
                review.Status = PlayerLinkReviewStatus.Resolved;
                review.UpdatedAt = now;
            }
            await db.SaveChangesAsync();
            return PlayerLinkOutcome.Linked;
        }

        // Otherwise queue for admin resolution. Best-guess = a single (out-of-alliance) match, else none.
        var candidateId = candidates.Count == 1 ? candidates[0].Id : (int?)null;
        if (review is null)
        {
            db.PlayerLinkReviews.Add(new PlayerLinkReview
            {
                GuildId = guildId,
                GuildAllianceId = guildAllianceId,
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
            // Refresh the snapshot (nickname/candidate may have changed); keep an in-flight DmSent
            // status so MemberOnboarding doesn't re-DM a member it's already reached out to.
            review.GuildAllianceId = guildAllianceId;
            review.Nickname = nickname;
            review.CandidateStfcPlayerId = candidateId;
            review.UpdatedAt = now;
        }
        await db.SaveChangesAsync();
        return PlayerLinkOutcome.Queued;
    }

    // Admin table action: link the member to the chosen player and mark their review Resolved.
    public async Task ResolveReviewAsync(int reviewId, int stfcPlayerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var review = await db.PlayerLinkReviews.FindAsync(reviewId);
        if (review is null)
            return;

        await LinkAsync(db, review.DiscordUserId, stfcPlayerId);
        review.Status = PlayerLinkReviewStatus.Resolved;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    // Admin table action: dismiss a review without linking (never re-processed).
    public async Task IgnoreReviewAsync(int reviewId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var review = await db.PlayerLinkReviews.FindAsync(reviewId);
        if (review is null)
            return;

        review.Status = PlayerLinkReviewStatus.Ignored;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
}

public enum PlayerLinkOutcome
{
    Linked,          // confident single in-alliance match → silently linked
    Queued,          // ambiguous/no match → Unresolved review row for the admin table
    AlreadyLinked,   // member already has a UserPlayer link
    AlreadyResolved, // member already has a terminal review (or a misconfigured alliance link)
}
