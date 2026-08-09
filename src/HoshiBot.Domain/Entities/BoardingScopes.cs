namespace HoshiBot.Domain.Entities;

// Which enabled Boarding scope claims a given member — the rule that keeps a member from being
// boarded twice in a Discord that runs Boarding for both an alliance and its wider community.
//
// Narrowest first. An Alliance scope claims only the members whose linked player is in THAT
// alliance; Server, Veil Group and Community claim everyone who joins, linked or not. So an
// alliance member in a community Discord gets their alliance's welcome, and everyone else falls
// through to the broader one — which is what an admin means by enabling both.
//
// Pure and here rather than in the service because it is the one piece of Boarding with a decision
// in it, and it is worth being able to test without a Discord or a database.
public static class BoardingScopes
{
    // Narrowest to broadest. Not the GuildAudience enum's own order — that is a flags layout, and
    // reading a priority out of bit positions would be an accident waiting to be renumbered.
    public static readonly IReadOnlyList<GuildAudience> Order =
        [GuildAudience.Alliance, GuildAudience.Server, GuildAudience.VeilGroup, GuildAudience.Community];

    public readonly record struct Scope(GuildAudience Audience, int? GuildAllianceId);

    // The scope that boards this member, or null if none does. memberGuildAllianceId is the linked
    // alliance they belong to in this guild (null when unlinked, or linked to an alliance the guild
    // does not track).
    public static Scope? Claim(IReadOnlyCollection<Scope> enabled, int? memberGuildAllianceId)
    {
        foreach (var audience in Order)
        {
            foreach (var scope in enabled.Where(s => s.Audience == audience))
            {
                // An alliance boards its own. Everyone else's scopes board whoever turns up: a
                // server or community Discord has no in-game membership to check against, which is
                // the whole reason the link test is Alliance-only.
                if (audience != GuildAudience.Alliance)
                    return scope;

                if (memberGuildAllianceId is { } allianceId && scope.GuildAllianceId == allianceId)
                    return scope;
            }
        }

        return null;
    }
}
