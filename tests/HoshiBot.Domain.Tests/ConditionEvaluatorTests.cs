using HoshiBot.Domain.ConditionalRoles;
using Xunit;

namespace HoshiBot.Domain.Tests;

public class ConditionEvaluatorTests
{
    private const ulong Server164 = 100;
    private const ulong TagKaos = 200;
    private const ulong TagBctk = 201;
    private const ulong Guest = 300;

    private static readonly Dictionary<int, ConditionNode> NoConditions = [];

    private static MemberFacts Member(params ulong[] roles) => MemberFacts.FromRoles(roles);

    // A member the bot has player data for.
    private static MemberFacts Linked(bool inHomeAlliance, bool onHomeServer, params ulong[] roles) =>
        new(roles.ToHashSet(), new PlayerFacts(inHomeAlliance, onHomeServer));

    private static ConditionOutcome Eval(ConditionNode node, MemberFacts facts, Dictionary<int, ConditionNode>? conditions = null) =>
        ConditionEvaluator.Evaluate(node, facts, conditions ?? NoConditions);

    // Most cases are about "does this grant the role", where Unknown and NoMatch are both "no" —
    // the tests that care about the difference assert on the outcome directly.
    private static bool Grants(ConditionNode node, MemberFacts facts, Dictionary<int, ConditionNode>? conditions = null) =>
        Eval(node, facts, conditions) == ConditionOutcome.Match;

    [Fact]
    public void HasRole_MatchesOnlyWhenHeld()
    {
        Assert.True(Grants(ConditionNode.HasRole(Server164), Member(Server164)));
        Assert.False(Grants(ConditionNode.HasRole(Server164), Member(Guest)));
        Assert.False(Grants(ConditionNode.HasRole(Server164), Member()));
    }

    [Fact]
    public void And_RequiresEveryChild()
    {
        var node = ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.HasRole(TagKaos));

        Assert.True(Grants(node, Member(Server164, TagKaos)));
        Assert.False(Grants(node, Member(Server164)));
        Assert.False(Grants(node, Member(TagKaos)));
    }

    [Fact]
    public void Or_NeedsOneChild()
    {
        var node = ConditionNode.Or(ConditionNode.HasRole(TagKaos), ConditionNode.HasRole(TagBctk));

        Assert.True(Grants(node, Member(TagKaos)));
        Assert.True(Grants(node, Member(TagBctk)));
        Assert.True(Grants(node, Member(TagKaos, TagBctk)));
        Assert.False(Grants(node, Member(Server164)));
    }

    [Fact]
    public void Not_InvertsItsSingleChild()
    {
        var node = ConditionNode.Not(ConditionNode.HasRole(Guest));

        Assert.True(Grants(node, Member(Server164)));
        Assert.False(Grants(node, Member(Guest)));
    }

    // The motivating pair: "on 164 and carries one of our tags" grants one role, the same with NOT
    // grants the other, and a member must land in exactly one of them.
    [Fact]
    public void MotivatingRulePair_IsMutuallyExclusive()
    {
        var tags = ConditionNode.Or(ConditionNode.HasRole(TagKaos), ConditionNode.HasRole(TagBctk));
        var verified = ConditionNode.And(ConditionNode.HasRole(Server164), tags);
        var guest = ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.Not(tags));

        var tagged = Member(Server164, TagKaos);
        Assert.True(Grants(verified, tagged));
        Assert.False(Grants(guest, tagged));

        var untagged = Member(Server164);
        Assert.False(Grants(verified, untagged));
        Assert.True(Grants(guest, untagged));

        // Not on 164 at all: neither rule applies, so neither role is granted.
        var outsider = Member(TagKaos);
        Assert.False(Grants(verified, outsider));
        Assert.False(Grants(guest, outsider));
    }

    // Every one of these is a shape an admin can leave behind mid-edit. None may grant the role,
    // and none may be reported complete.
    [Theory]
    [MemberData(nameof(IncompleteNodes))]
    public void IncompleteNodes_GrantNothing(ConditionNode node)
    {
        Assert.False(ConditionEvaluator.IsComplete(node, NoConditions));
        Assert.False(Grants(node, Member(Server164, TagKaos)));
        Assert.False(Grants(node, Member()));
    }

    public static TheoryData<ConditionNode> IncompleteNodes() =>
    [
        ConditionNode.And(),
        ConditionNode.Or(),
        new ConditionNode(ConditionNodeKind.Not, []),
        new ConditionNode(ConditionNodeKind.HasRole, []),
        new ConditionNode(ConditionNodeKind.MatchesCondition, []),
        // The case that forced completeness to be checked separately from truth: an empty group is
        // false, so NOT of it is true, so this whole rule would match every member holding @164 —
        // the exact "granted the role to the entire guild" accident fail-closed exists to prevent.
        ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.Not(ConditionNode.And())),
        // Unfinished leaf buried deep enough that the operators above it all look healthy.
        ConditionNode.Or(ConditionNode.HasRole(TagKaos), ConditionNode.And(new ConditionNode(ConditionNodeKind.HasRole, []))),
        // A Not with two children is ambiguous between NOT(a AND b) and NOT(a OR b) — the editor
        // never builds one, and guessing is worse than refusing.
        new ConditionNode(ConditionNodeKind.Not, [ConditionNode.HasRole(Guest), ConditionNode.HasRole(TagKaos)]),
    ];

    [Fact]
    public void CompleteTree_IsReportedComplete()
    {
        var conditions = new Dictionary<int, ConditionNode> { [7] = ConditionNode.HasRole(TagKaos) };
        var node = ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.Not(ConditionNode.Matches(7)));

        Assert.True(ConditionEvaluator.IsComplete(node, conditions));
    }

    // Incompleteness has to survive negation: it is not a truth value that can be flipped.
    [Fact]
    public void Not_OfAnIncompleteSubtree_StillGrantsNothing()
    {
        var conditions = new Dictionary<int, ConditionNode>();
        var node = ConditionNode.Not(ConditionNode.Matches(404));

        Assert.False(ConditionEvaluator.IsComplete(node, conditions));
        Assert.False(Grants(node, Member(Server164), conditions));
    }

    [Fact]
    public void MatchesCondition_ResolvesTheNamedTree()
    {
        var conditions = new Dictionary<int, ConditionNode>
        {
            [7] = ConditionNode.Or(ConditionNode.HasRole(TagKaos), ConditionNode.HasRole(TagBctk)),
        };
        var node = ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.Matches(7));

        Assert.True(Grants(node, Member(Server164, TagBctk), conditions));
        Assert.False(Grants(node, Member(Server164), conditions));
    }

    [Fact]
    public void MatchesCondition_MissingConditionIsFalse()
    {
        // The condition was deleted (or belongs to another guild) — the rule stops matching rather
        // than silently dropping that part of the expression.
        var node = ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.Matches(999));

        Assert.False(Grants(node, Member(Server164)));
    }

    [Fact]
    public void MatchesCondition_DirectCycleIsFalse()
    {
        var conditions = new Dictionary<int, ConditionNode> { [1] = ConditionNode.Matches(1) };

        Assert.False(Grants(ConditionNode.Matches(1), Member(Server164), conditions));
    }

    [Fact]
    public void MatchesCondition_IndirectCycleIsFalse()
    {
        var conditions = new Dictionary<int, ConditionNode>
        {
            [1] = ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.Matches(2)),
            [2] = ConditionNode.Matches(3),
            [3] = ConditionNode.Matches(1),
        };

        Assert.False(Grants(ConditionNode.Matches(1), Member(Server164), conditions));
    }

    [Fact]
    public void MatchesCondition_SameConditionTwiceIsNotACycle()
    {
        // Referencing one condition from two branches is ordinary reuse — the cycle guard has to
        // unwind on the way out, or the second reference would read as a loop.
        var conditions = new Dictionary<int, ConditionNode> { [1] = ConditionNode.HasRole(TagKaos) };
        var node = ConditionNode.And(ConditionNode.Matches(1), ConditionNode.Or(ConditionNode.Matches(1)));

        Assert.True(Grants(node, Member(TagKaos), conditions));
    }

    [Fact]
    public void DeepNesting_Evaluates()
    {
        // ((164 AND (KAOS OR BCTK)) AND NOT Guest)
        var node = ConditionNode.And(
            ConditionNode.And(
                ConditionNode.HasRole(Server164),
                ConditionNode.Or(ConditionNode.HasRole(TagKaos), ConditionNode.HasRole(TagBctk))),
            ConditionNode.Not(ConditionNode.HasRole(Guest)));

        Assert.True(Grants(node, Member(Server164, TagKaos)));
        Assert.False(Grants(node, Member(Server164, TagKaos, Guest)));
        Assert.False(Grants(node, Member(Server164)));
    }

    [Fact]
    public void UnknownKind_IsFalse()
    {
        // A row written by a newer build with a leaf kind this one doesn't know.
        var node = new ConditionNode((ConditionNodeKind)99, []);

        Assert.False(Grants(node, Member(Server164, TagKaos)));
    }

    [Fact]
    public void HasLinkedPlayer_IsAnswerableEitherWay()
    {
        var node = ConditionNode.Leaf(ConditionNodeKind.HasLinkedPlayer);

        // Never Unknown — which is exactly what lets it guard the two facts that can be.
        Assert.Equal(ConditionOutcome.Match, Eval(node, Linked(inHomeAlliance: true, onHomeServer: true)));
        Assert.Equal(ConditionOutcome.NoMatch, Eval(node, Member()));
    }

    [Theory]
    [InlineData(ConditionNodeKind.InHomeAlliance)]
    [InlineData(ConditionNodeKind.OnHomeServer)]
    public void PlayerFacts_AreUnknownWithoutALinkedPlayer(ConditionNodeKind kind)
    {
        var node = ConditionNode.Leaf(kind);

        Assert.Equal(ConditionOutcome.Match, Eval(node, Linked(inHomeAlliance: true, onHomeServer: true)));
        Assert.Equal(ConditionOutcome.NoMatch, Eval(node, Linked(inHomeAlliance: false, onHomeServer: false)));

        // The distinction the whole three-valued design exists for: nobody linked means no answer,
        // NOT "no". The caller leaves such a member exactly as they are.
        Assert.Equal(ConditionOutcome.Unknown, Eval(node, Member()));
    }

    // If Unknown were false, this would grant the role to every member whose player data hasn't
    // imported yet — the negation trap, in its second form.
    [Fact]
    public void Not_OfAnUnknownFact_StaysUnknown()
    {
        var node = ConditionNode.Not(ConditionNode.Leaf(ConditionNodeKind.InHomeAlliance));

        Assert.Equal(ConditionOutcome.Unknown, Eval(node, Member()));
        Assert.Equal(ConditionOutcome.Match, Eval(node, Linked(inHomeAlliance: false, onHomeServer: true)));
        Assert.Equal(ConditionOutcome.NoMatch, Eval(node, Linked(inHomeAlliance: true, onHomeServer: true)));
    }

    [Fact]
    public void And_ADefiniteFailureBeatsAnUnknownBesideIt()
    {
        // Not holding @164 settles it regardless of what we don't know about their player.
        var node = ConditionNode.And(ConditionNode.HasRole(Server164), ConditionNode.Leaf(ConditionNodeKind.InHomeAlliance));

        Assert.Equal(ConditionOutcome.NoMatch, Eval(node, Member()));
        // Holding it leaves the answer resting on the fact we don't have.
        Assert.Equal(ConditionOutcome.Unknown, Eval(node, Member(Server164)));
    }

    [Fact]
    public void Or_ADefiniteMatchBeatsAnUnknownBesideIt()
    {
        var node = ConditionNode.Or(ConditionNode.HasRole(Server164), ConditionNode.Leaf(ConditionNodeKind.InHomeAlliance));

        Assert.Equal(ConditionOutcome.Match, Eval(node, Member(Server164)));
        Assert.Equal(ConditionOutcome.Unknown, Eval(node, Member(Guest)));
    }

    // Guarding with HasLinkedPlayer is how an admin turns an Unknown into a decided answer.
    [Fact]
    public void HasLinkedPlayerGuard_RemovesTheUnknown()
    {
        var rogues = ConditionNode.Or(
            ConditionNode.Not(ConditionNode.Leaf(ConditionNodeKind.HasLinkedPlayer)),
            ConditionNode.And(
                ConditionNode.Leaf(ConditionNodeKind.HasLinkedPlayer),
                ConditionNode.Not(ConditionNode.Leaf(ConditionNodeKind.InHomeAlliance))));

        // Nobody linked: the first branch decides it, so no Unknown survives.
        Assert.Equal(ConditionOutcome.Match, Eval(rogues, Member()));
        // Linked and foreign.
        Assert.Equal(ConditionOutcome.Match, Eval(rogues, Linked(inHomeAlliance: false, onHomeServer: true)));
        // Linked and one of ours.
        Assert.Equal(ConditionOutcome.NoMatch, Eval(rogues, Linked(inHomeAlliance: true, onHomeServer: true)));
    }

    [Fact]
    public void PlayerFactLeaves_AreAlwaysComplete()
    {
        // They take no operand, so there is nothing an admin can leave half-filled.
        foreach (var kind in new[] { ConditionNodeKind.HasLinkedPlayer, ConditionNodeKind.InHomeAlliance, ConditionNodeKind.OnHomeServer })
            Assert.True(ConditionEvaluator.IsComplete(ConditionNode.Leaf(kind), NoConditions));
    }
}
