namespace HoshiBot.Domain.ConditionalRoles;

// What the bot knows about the player behind a member. Absent when nobody is linked — the type
// makes that the only way to express it, so "we have no player data" can't be mistaken for "their
// player is in no alliance", which is a different and knowable thing.
//
// The two flags are relative to the guild's own scope (GuildServerScope): its linked alliances and
// their servers. Deliberately not the alliance/server ids themselves — a rule saying "one of ours"
// keeps working when the guild links another alliance, where a rule naming an id would quietly go
// stale.
public readonly record struct PlayerFacts(int PlayerId, int? AllianceId, bool InHomeAlliance, bool OnHomeServer);

// Everything a condition can ask about one member: the Discord roles they hold, and their player
// data when there is any.
public readonly record struct MemberFacts(IReadOnlySet<ulong> RoleIds, PlayerFacts? Player = null)
{
    public bool HasLinkedPlayer => Player is not null;

    public static MemberFacts FromRoles(IEnumerable<ulong> roleIds) => new(roleIds.ToHashSet());
}
