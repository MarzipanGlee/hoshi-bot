namespace HoshiBot.Domain.ConditionalRoles;

// What a rule concluded about one member.
public enum ConditionOutcome
{
    // Definitely does not satisfy the rule — the role should come off.
    NoMatch,

    // Definitely satisfies it — the role should go on.
    Match,

    // Can't be decided, because the rule asks about player data this member doesn't have. The
    // caller must leave them exactly as they are: neither granting nor removing.
    Unknown,
}

// Decides whether one member satisfies a condition tree.
//
// Two things here refuse to be truth values, and both for the same reason — negation:
//
//   UNFINISHED. An And with no children is vacuously true, and even defining it as false doesn't
//   help, because a Not above it flips it back. An admin who adds an empty "NOT (…)" group and
//   walks away would grant the role to the whole guild. So IsComplete walks the tree first and any
//   unfinished node anywhere makes the rule match nobody, whatever the logic above it says.
//
//   UNKNOWN. "Is this member in one of our alliances?" has no answer when nobody is linked to them.
//   Treating that as false means `not in one of our alliances` matches every member whose player
//   data hasn't imported yet. So it propagates as its own outcome (Kleene three-valued logic) and
//   the caller leaves those members alone.
//
// Fail-closed throughout: when in doubt, don't grant.
public static class ConditionEvaluator
{
    public static ConditionOutcome Evaluate(
        ConditionNode node,
        MemberFacts facts,
        IReadOnlyDictionary<int, ConditionNode> namedConditions) =>
        IsComplete(node, namedConditions)
            ? EvaluateCore(node, facts, namedConditions)
            : ConditionOutcome.NoMatch;

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

            // The player-data leaves take no operand, so there is nothing to leave unfinished.
            ConditionNodeKind.HasLinkedPlayer or ConditionNodeKind.InHomeAlliance or ConditionNodeKind.OnHomeServer => true,

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

    // Kleene three-valued logic. Only ever reached for a tree IsComplete has already accepted, so
    // the degenerate shapes it defends against cannot occur here.
    private static ConditionOutcome EvaluateCore(
        ConditionNode node,
        MemberFacts facts,
        IReadOnlyDictionary<int, ConditionNode> namedConditions) => node.Kind switch
        {
            // A definite failure settles an And even with unknowns beside it; only when everything
            // else passes does an unknown leave the answer open. Mirror image for Or.
            ConditionNodeKind.And => Combine(node, facts, namedConditions, decisive: ConditionOutcome.NoMatch),
            ConditionNodeKind.Or => Combine(node, facts, namedConditions, decisive: ConditionOutcome.Match),

            ConditionNodeKind.Not => EvaluateCore(node.Children[0], facts, namedConditions) switch
            {
                ConditionOutcome.Match => ConditionOutcome.NoMatch,
                ConditionOutcome.NoMatch => ConditionOutcome.Match,
                // Not knowing stays not knowing — this is the whole reason unknown isn't `false`.
                _ => ConditionOutcome.Unknown,
            },

            ConditionNodeKind.HasRole => Known(facts.RoleIds.Contains(node.RoleId!.Value)),

            ConditionNodeKind.MatchesCondition =>
                EvaluateCore(namedConditions[node.ReferencedConditionId!.Value], facts, namedConditions),

            // Always answerable, which is what makes it usable as a guard in front of the two below.
            ConditionNodeKind.HasLinkedPlayer => Known(facts.HasLinkedPlayer),

            ConditionNodeKind.InHomeAlliance => facts.Player is { } inAlliance
                ? Known(inAlliance.InHomeAlliance)
                : ConditionOutcome.Unknown,

            ConditionNodeKind.OnHomeServer => facts.Player is { } onServer
                ? Known(onServer.OnHomeServer)
                : ConditionOutcome.Unknown,

            _ => ConditionOutcome.NoMatch,
        };

    private static ConditionOutcome Combine(
        ConditionNode node,
        MemberFacts facts,
        IReadOnlyDictionary<int, ConditionNode> namedConditions,
        ConditionOutcome decisive)
    {
        var sawUnknown = false;
        foreach (var child in node.Children)
        {
            var outcome = EvaluateCore(child, facts, namedConditions);
            if (outcome == decisive)
                return decisive;
            if (outcome == ConditionOutcome.Unknown)
                sawUnknown = true;
        }

        return sawUnknown
            ? ConditionOutcome.Unknown
            : decisive == ConditionOutcome.NoMatch ? ConditionOutcome.Match : ConditionOutcome.NoMatch;
    }

    private static ConditionOutcome Known(bool value) => value ? ConditionOutcome.Match : ConditionOutcome.NoMatch;
}
