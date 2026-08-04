namespace HoshiBot.Domain.ConditionalRoles;

// What one node of a condition tree does. Operators take children; leaves take an operand.
//
// The leaf set is deliberately open: only HasRole ships, but every leaf carries its kind, so rank /
// ops level / server / alliance / has-linked-player can be added as new members later without
// reshaping the node model or its storage. Append new members at the end — the value is persisted
// as an int on ConditionalRoleNode.
public enum ConditionNodeKind
{
    And,
    Or,
    Not,
    HasRole,
    MatchesCondition,

    // Player-data leaves. None of them takes an operand: "one of ours" is asked against the guild's
    // own scope (GuildServerScope) rather than a named alliance or server, so a rule keeps meaning
    // what it said when the guild links another alliance.
    //
    // The last two are UNKNOWN for a member with no linked player, which is not the same as false —
    // see ConditionEvaluator's three-valued logic. HasLinkedPlayer is always answerable, which is
    // what makes it usable as a guard.
    HasLinkedPlayer,
    InHomeAlliance,
    OnHomeServer,

    // Whether the member's linked player IS one particular player. Unlike the two above this is
    // never Unknown: it asks about the link itself, and "nobody is linked" answers it definitively.
    IsPlayer,
}

// One node of a condition tree, free of EF and Discord so the semantics can be unit-tested on its
// own (same spirit as NicknameComposer / AllianceTagRoleName).
//
// RoleId is set for HasRole, ReferencedConditionId for MatchesCondition, Children for the operators
// — a node only ever uses the one that belongs to its Kind, and the evaluator treats a node whose
// operand is missing as false rather than trusting it (see ConditionEvaluator).
public sealed record ConditionNode(
    ConditionNodeKind Kind,
    IReadOnlyList<ConditionNode> Children,
    ulong? RoleId = null,
    int? ReferencedConditionId = null,
    int? StfcPlayerId = null)
{
    public static ConditionNode And(params ConditionNode[] children) => new(ConditionNodeKind.And, children);

    public static ConditionNode Or(params ConditionNode[] children) => new(ConditionNodeKind.Or, children);

    public static ConditionNode Not(ConditionNode child) => new(ConditionNodeKind.Not, [child]);

    public static ConditionNode HasRole(ulong roleId) => new(ConditionNodeKind.HasRole, [], RoleId: roleId);

    public static ConditionNode Matches(int conditionId) =>
        new(ConditionNodeKind.MatchesCondition, [], ReferencedConditionId: conditionId);

    public static ConditionNode Leaf(ConditionNodeKind kind) => new(kind, []);

    public static ConditionNode Player(int stfcPlayerId) =>
        new(ConditionNodeKind.IsPlayer, [], StfcPlayerId: stfcPlayerId);
}
