using HoshiBot.Domain.ConditionalRoles;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features.ConditionalRoles;

// The mutable, half-finished counterpart of ConditionNode: what the tree editor binds to while an
// admin is still building it. ConditionNode is an immutable record with the operand for its kind
// already decided, which is exactly wrong for a form where the kind changes under you and the
// operand is briefly blank.
//
// RoleId is a string because that is what RolePicker binds to (and what carries its "create"/unknown
// sentinels); it becomes a real id only in ToDomain.
public sealed class EditableNode
{
    public ConditionNodeKind Kind { get; set; } = ConditionNodeKind.HasRole;

    public string? RoleId { get; set; }

    public int? ReferencedConditionId { get; set; }

    public int? StfcPlayerId { get; set; }

    public List<EditableNode> Children { get; } = [];

    public bool IsOperator => Kind is ConditionNodeKind.And or ConditionNodeKind.Or or ConditionNodeKind.Not;

    public static EditableNode NewGroup() => new() { Kind = ConditionNodeKind.And };

    public static EditableNode FromDomain(ConditionNode node)
    {
        var editable = new EditableNode
        {
            Kind = node.Kind,
            RoleId = node.RoleId?.ToString(),
            ReferencedConditionId = node.ReferencedConditionId,
            StfcPlayerId = node.StfcPlayerId,
        };
        foreach (var child in node.Children)
            editable.Children.Add(FromDomain(child));
        return editable;
    }

    // Carries the half-finished state through as-is rather than dropping incomplete parts: a leaf
    // with no role stays a leaf with no role, so ConditionEvaluator.IsComplete sees the tree the
    // admin actually built and the rule grants nothing until they finish it. Silently pruning the
    // blanks here would turn an unfinished rule into a smaller, complete — and wrong — one.
    public ConditionNode ToDomain() => new(
        Kind,
        Children.Select(c => c.ToDomain()).ToList(),
        ulong.TryParse(RoleId, out var roleId) ? roleId : null,
        ReferencedConditionId,
        StfcPlayerId);
}
