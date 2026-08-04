using HoshiBot.Domain.ConditionalRoles;

namespace HoshiBot.Domain.Entities;

// One node of a stored condition tree — the persisted form of ConditionalRoles.ConditionNode, which
// is the shape the evaluator actually works on. Kept as rows rather than a serialized blob so that
// "which rules reference this role / this condition?" stays a query, which the editor needs before
// it lets anything be deleted.
//
// A tree is always read and written whole, so saving is delete-every-node-for-this-owner then
// reinsert. That is why ParentId needs no cascade and why Position is a plain int.
public class ConditionalRoleNode
{
    public int Id { get; set; }

    // Exactly one owner is set: the node belongs either to a rule's tree or to a named condition's
    // tree. Two nullable FKs to different parents rather than one polymorphic column, so the
    // database still enforces that the owner exists — and since a given row only ever has one of
    // them, the two cascade paths never both reach it.
    public int? OwnerRuleId { get; set; }

    public ConditionalRoleRule? OwnerRule { get; set; }

    public int? OwnerConditionId { get; set; }

    public ConditionalRoleCondition? OwnerCondition { get; set; }

    // Null for the root of the owner's tree.
    public int? ParentId { get; set; }

    public ConditionalRoleNode? Parent { get; set; }

    public ICollection<ConditionalRoleNode> Children { get; set; } = [];

    public ConditionNodeKind Kind { get; set; }

    // Set only for Kind == HasRole.
    public ulong? RoleId { get; set; }

    // Set only for Kind == MatchesCondition — the condition this node defers to, which is a
    // different thing from OwnerConditionId (the condition this node is part of).
    public int? ReferencedConditionId { get; set; }

    public ConditionalRoleCondition? ReferencedCondition { get; set; }

    // Set only for Kind == IsPlayer. SetNull if the player ever leaves the catalog: the node then
    // reads as unfinished, which makes the whole rule grant nothing — visibly harmless rather than
    // quietly matching the wrong person if an id were ever reused.
    public int? StfcPlayerId { get; set; }

    public StfcPlayer? StfcPlayer { get; set; }

    // Sibling order, so a rebuilt tree reads back the way the admin arranged it. Only meaningful
    // among nodes sharing a parent.
    public int Position { get; set; }
}
