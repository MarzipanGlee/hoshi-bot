namespace HoshiBot.Domain.ConditionalRoles;

// Decides whether one member satisfies a condition tree.
//
// FAIL-CLOSED IS THE RULE HERE, and it is why completeness is checked separately from truth rather
// than folded into it. Boolean logic alone gets this wrong in one specific, dangerous way: an And
// with no children is vacuously TRUE, and even if you define it as false, wrapping it in a Not
// flips it back to true — so an admin who adds an empty "NOT (…)" group and walks away would grant
// the target role to the whole guild. Negation means "unfinished" cannot be expressed as a truth
// value at all; it has to short-circuit the entire rule.
//
// So: IsComplete walks the tree first and any unfinished or unresolvable node anywhere makes the
// whole rule match nobody, whatever the logic above it would have done. The editor calls the same
// method to tell an admin their rule currently grants nothing.
public static class ConditionEvaluator
{
    // True only when the tree is fully built AND the member satisfies it.
    public static bool Evaluate(
        ConditionNode node,
        MemberFacts facts,
        IReadOnlyDictionary<int, ConditionNode> namedConditions) =>
        IsComplete(node, namedConditions) && EvaluateCore(node, facts, namedConditions);

    // Whether every node is usable: operators have the children they need, leaves have their
    // operand, and every referenced condition exists and is itself complete and acyclic. An
    // incomplete tree is not an error — it's the normal state of a rule someone is still building —
    // so this reports it rather than throwing.
    public static bool IsComplete(ConditionNode node, IReadOnlyDictionary<int, ConditionNode> namedConditions) =>
        IsComplete(node, namedConditions, []);

    private static bool IsComplete(
        ConditionNode node,
        IReadOnlyDictionary<int, ConditionNode> namedConditions,
        HashSet<int> visiting) => node.Kind switch
        {
            // An operator with no children expresses no opinion, which is not the same as "everyone".
            ConditionNodeKind.And or ConditionNodeKind.Or =>
                node.Children.Count > 0 && node.Children.All(c => IsComplete(c, namedConditions, visiting)),

            // Exactly one child: a Not with two would be ambiguous between NOT(a AND b) and
            // NOT(a OR b), and guessing is worse than refusing.
            ConditionNodeKind.Not =>
                node.Children.Count == 1 && IsComplete(node.Children[0], namedConditions, visiting),

            ConditionNodeKind.HasRole => node.RoleId is not null,

            ConditionNodeKind.MatchesCondition => IsReferenceComplete(node, namedConditions, visiting),

            // A kind this build doesn't know (a row written by a newer version) is not something to
            // guess at.
            _ => false,
        };

    private static bool IsReferenceComplete(
        ConditionNode node,
        IReadOnlyDictionary<int, ConditionNode> namedConditions,
        HashSet<int> visiting)
    {
        // Missing means deleted, or belonging to another guild — either way the rule stops matching
        // instead of silently dropping that part of the expression.
        if (node.ReferencedConditionId is not { } conditionId
            || !namedConditions.TryGetValue(conditionId, out var condition)
            || !visiting.Add(conditionId))
        {
            return false;
        }

        try
        {
            return IsComplete(condition, namedConditions, visiting);
        }
        finally
        {
            // Unwound on the way out so a condition referenced from two branches still resolves the
            // second time — only a reference still being expanded is a genuine cycle.
            visiting.Remove(conditionId);
        }
    }

    // Plain boolean logic; only ever reached for a tree IsComplete has already accepted, so the
    // degenerate shapes it would otherwise have to defend against cannot occur here.
    private static bool EvaluateCore(
        ConditionNode node,
        MemberFacts facts,
        IReadOnlyDictionary<int, ConditionNode> namedConditions) => node.Kind switch
        {
            ConditionNodeKind.And => node.Children.All(c => EvaluateCore(c, facts, namedConditions)),
            ConditionNodeKind.Or => node.Children.Any(c => EvaluateCore(c, facts, namedConditions)),
            ConditionNodeKind.Not => !EvaluateCore(node.Children[0], facts, namedConditions),
            ConditionNodeKind.HasRole => facts.RoleIds.Contains(node.RoleId!.Value),
            ConditionNodeKind.MatchesCondition =>
                EvaluateCore(namedConditions[node.ReferencedConditionId!.Value], facts, namedConditions),
            _ => false,
        };
}
