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
    int? ReferencedConditionId = null)
{
    public static ConditionNode And(params ConditionNode[] children) => new(ConditionNodeKind.And, children);

    public static ConditionNode Or(params ConditionNode[] children) => new(ConditionNodeKind.Or, children);

    public static ConditionNode Not(ConditionNode child) => new(ConditionNodeKind.Not, [child]);

    public static ConditionNode HasRole(ulong roleId) => new(ConditionNodeKind.HasRole, [], RoleId: roleId);

    public static ConditionNode Matches(int conditionId) =>
        new(ConditionNodeKind.MatchesCondition, [], ReferencedConditionId: conditionId);
}
