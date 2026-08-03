namespace HoshiBot.Domain.ConditionalRoles;

// Everything a condition can ask about one member. Only the Discord roles they hold today — the
// point of naming it "facts" rather than "roles" is that rank, ops level, server, alliance and
// "has a linked player" land here later next to a matching ConditionNodeKind, without the evaluator
// or the storage changing shape.
public readonly record struct MemberFacts(IReadOnlySet<ulong> RoleIds)
{
    public static MemberFacts FromRoles(IEnumerable<ulong> roleIds) => new(roleIds.ToHashSet());
}
