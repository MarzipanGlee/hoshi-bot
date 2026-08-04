using HoshiBot.Domain.ConditionalRoles;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// One rule as the sync sees it: its target role and the evaluable tree, or null when the rule has
// no tree at all yet (a rule that grants nothing — see ConditionEvaluator's fail-closed contract).
public record ConditionalRuleTree(int Id, string Name, ulong TargetRoleId, ConditionNode? Root);

// Everything the sync needs for one guild, read in one go: the enabled rules and every named
// condition, since a rule can reference any of them.
public record ConditionalRoleSnapshot(
    IReadOnlyList<ConditionalRuleTree> Rules,
    IReadOnlyDictionary<int, ConditionNode> Conditions);

// One choice in an operand typeahead — a player or an alliance, already rendered for display.
public record OperandOption(int Id, string Label);

// Reads and writes condition trees, converting between the stored rows (ConditionalRoleNode) and
// the pure evaluable shape (ConditionNode) the Domain evaluator works on. Nothing here decides
// whether a member matches — that is ConditionEvaluator's job, deliberately kept free of EF.
public class ConditionalRoleService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    // Everything the sync job needs for one guild. onlyEnabledRules is what the job passes; the
    // editor wants the disabled ones too.
    public async Task<ConditionalRoleSnapshot> LoadAsync(ulong guildId, bool onlyEnabledRules = true)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var rules = await db.ConditionalRoleRules
            .Where(r => r.GuildId == guildId && (!onlyEnabledRules || r.Enabled))
            .OrderBy(r => r.Name)
            .ToListAsync();

        var conditions = await db.ConditionalRoleConditions
            .Where(c => c.GuildId == guildId)
            .ToListAsync();

        var ruleIds = rules.Select(r => r.Id).ToHashSet();
        var conditionIds = conditions.Select(c => c.Id).ToHashSet();

        var nodes = await db.ConditionalRoleNodes
            .Where(n => (n.OwnerRuleId != null && ruleIds.Contains(n.OwnerRuleId.Value))
                || (n.OwnerConditionId != null && conditionIds.Contains(n.OwnerConditionId.Value)))
            .OrderBy(n => n.Position)
            .ToListAsync();

        var byParent = nodes.Where(n => n.ParentId is not null).ToLookup(n => n.ParentId!.Value);

        ConditionNode? RootOf(Func<ConditionalRoleNode, bool> ownedByThis) =>
            nodes.FirstOrDefault(n => n.ParentId is null && ownedByThis(n)) is { } root ? Build(root, byParent) : null;

        return new ConditionalRoleSnapshot(
            rules.Select(r => new ConditionalRuleTree(r.Id, r.Name, r.TargetRoleId, RootOf(n => n.OwnerRuleId == r.Id))).ToList(),
            conditions.ToDictionary(c => c.Id, c => Build(
                nodes.FirstOrDefault(n => n.ParentId is null && n.OwnerConditionId == c.Id), byParent)
                ?? ConditionNode.And()));
    }

    // A condition with no tree becomes an empty And, which IsComplete rejects — so referencing an
    // unfinished condition makes the referencing rule grant nothing, rather than being skipped.
    private static ConditionNode? Build(ConditionalRoleNode? node, ILookup<int, ConditionalRoleNode> byParent) =>
        node is null
            ? null
            : new ConditionNode(
                node.Kind,
                byParent[node.Id].OrderBy(c => c.Position).Select(c => Build(c, byParent)!).ToList(),
                node.RoleId,
                node.ReferencedConditionId,
                node.StfcPlayerId,
                node.StfcAllianceId);

    // Replaces an owner's whole tree. Trees are small and always edited as a unit, so this is
    // delete-everything-then-reinsert rather than a diff.
    //
    // The delete goes through the change tracker rather than ExecuteDelete on purpose: ParentId is a
    // Restrict FK, so rows have to be deleted children-first, and EF orders a tracked RemoveRange by
    // its dependency graph. A single DELETE statement would trip the constraint on the root.
    public async Task SaveTreeAsync(int? ruleId, int? conditionId, ConditionNode? root)
    {
        if ((ruleId is null) == (conditionId is null))
            throw new ArgumentException("A tree belongs to exactly one owner — a rule or a condition, not both or neither.");

        await using var db = await dbFactory.CreateDbContextAsync();

        var existing = await db.ConditionalRoleNodes
            .Where(n => (ruleId != null && n.OwnerRuleId == ruleId) || (conditionId != null && n.OwnerConditionId == conditionId))
            .ToListAsync();
        db.ConditionalRoleNodes.RemoveRange(existing);
        await db.SaveChangesAsync();

        if (root is null)
            return;

        // Added as a graph via the Children navigation so EF inserts parents first and fills in the
        // generated ParentId itself.
        db.ConditionalRoleNodes.Add(ToEntity(root, ruleId, conditionId, position: 0));
        await db.SaveChangesAsync();
    }

    private static ConditionalRoleNode ToEntity(ConditionNode node, int? ruleId, int? conditionId, int position)
    {
        // Every row carries the owner, not just the root — the CK_ConditionalRoleNodes_SingleOwner
        // check constraint requires it, and it makes "load this owner's nodes" a single indexed read.
        var entity = new ConditionalRoleNode
        {
            OwnerRuleId = ruleId,
            OwnerConditionId = conditionId,
            Kind = node.Kind,
            RoleId = node.RoleId,
            ReferencedConditionId = node.ReferencedConditionId,
            StfcPlayerId = node.StfcPlayerId,
            StfcAllianceId = node.StfcAllianceId,
            Position = position,
        };

        for (var i = 0; i < node.Children.Count; i++)
            entity.Children.Add(ToEntity(node.Children[i], ruleId, conditionId, i));

        return entity;
    }

    // Whether saving this tree for this condition would create a reference cycle. Called by the
    // editor before saving so the admin gets told, rather than leaving the evaluator to notice at
    // sync time and silently grant nothing.
    public async Task<bool> WouldCycleAsync(ulong guildId, int conditionId, ConditionNode root)
    {
        var snapshot = await LoadAsync(guildId, onlyEnabledRules: false);
        var conditions = snapshot.Conditions.ToDictionary(kv => kv.Key, kv => kv.Value);
        conditions[conditionId] = root;

        return Reaches(root, conditionId, conditions, []);
    }

    private static bool Reaches(ConditionNode node, int target, IReadOnlyDictionary<int, ConditionNode> conditions, HashSet<int> seen)
    {
        if (node.Kind == ConditionNodeKind.MatchesCondition)
        {
            if (node.ReferencedConditionId is not { } id)
                return false;
            if (id == target)
                return true;
            if (!seen.Add(id))
                return false;
            return conditions.TryGetValue(id, out var referenced) && Reaches(referenced, target, conditions, seen);
        }

        return node.Children.Any(child => Reaches(child, target, conditions, seen));
    }

    // How many matches a typeahead offers at once. A server carries a couple of thousand players,
    // so the picker searches rather than listing; this is the same shape (and limit) the player
    // assignment page's typeahead already uses.
    private const int SearchLimit = 25;

    // Searches the WHOLE catalog, not just players linked to a member of this guild. That is the
    // point of the feature for a rogue listing: someone who left the Discord to hide is linked to
    // nobody, and naming them here is what lets the rule tag them the moment they rejoin under a new
    // name and PlayerLink links them again.
    // Searches the WHOLE catalog, not just players linked to a member of this guild. That is the
    // point of the feature for a rogue listing: someone who left the Discord to hide is linked to
    // nobody, and naming them here is what lets the rule tag them the moment they rejoin under a new
    // name and PlayerLink links them again.
    //
    // The label is built AFTER materializing. A method call in the projection is evaluated on the
    // client over entities whose Server/Alliance navigations were never loaded, so it threw an NRE
    // per keystroke — which killed the Blazor circuit and took the admin's unsaved tree with it.
    // Naming the columns inline is what makes EF join them.
    public async Task<List<OperandOption>> SearchPlayersAsync(string term, ulong guildId)
    {
        var t = term.Trim().ToLower();
        if (t.Length == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.StfcPlayers
            .Where(p => p.Name.ToLower().Contains(t))
            .OrderBy(p => p.Name)
            .Take(SearchLimit)
            .Select(p => new PlayerRow(p.Id, p.Name, p.Alliance != null ? p.Alliance.Tag : null, p.Server.Name))
            .ToListAsync();

        return rows.Select(r => new OperandOption(r.Id, PlayerLabel(r))).ToList();
    }

    public async Task<List<OperandOption>> SearchAlliancesAsync(string term, ulong guildId)
    {
        var t = term.Trim().ToLower();
        if (t.Length == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.StfcAlliances
            .Where(a => a.Name.ToLower().Contains(t) || a.Tag.ToLower().Contains(t))
            .OrderBy(a => a.Tag)
            .Take(SearchLimit)
            .Select(a => new AllianceRow(a.Id, a.Name, a.Tag, a.Server.Name))
            .ToListAsync();

        return rows.Select(r => new OperandOption(r.Id, AllianceLabel(r))).ToList();
    }

    // Labels for ids already stored in trees, so the editor can show what was picked without
    // re-searching for it. Resolved in one query per kind rather than one per node.
    public async Task<Dictionary<int, string>> PlayerLabelsAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.StfcPlayers
            .Where(p => ids.Contains(p.Id))
            .Select(p => new PlayerRow(p.Id, p.Name, p.Alliance != null ? p.Alliance.Tag : null, p.Server.Name))
            .ToListAsync();

        return rows.ToDictionary(r => r.Id, PlayerLabel);
    }

    public async Task<Dictionary<int, string>> AllianceLabelsAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.StfcAlliances
            .Where(a => ids.Contains(a.Id))
            .Select(a => new AllianceRow(a.Id, a.Name, a.Tag, a.Server.Name))
            .ToListAsync();

        return rows.ToDictionary(r => r.Id, AllianceLabel);
    }

    // Flat projections, so nothing here depends on a navigation being loaded.
    private record PlayerRow(int Id, string Name, string? AllianceTag, string ServerName);

    private record AllianceRow(int Id, string Name, string Tag, string ServerName);

    // Names repeat across servers and alliances, so both labels carry enough to tell two apart.
    private static string PlayerLabel(PlayerRow r) =>
        $"{(r.AllianceTag is null ? "" : $"[{r.AllianceTag}] ")}{r.Name} · {r.ServerName}";

    private static string AllianceLabel(AllianceRow r) => $"[{r.Tag}] {r.Name} · {r.ServerName}";

    // Which rules and conditions reference a condition — the reason the tree is stored as rows
    // rather than a blob. The editor shows these before letting a condition be deleted, since the
    // FK is Restrict and the delete would otherwise just fail with a database error.
    public async Task<(List<string> Rules, List<string> Conditions)> UsagesOfConditionAsync(int conditionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var nodes = await db.ConditionalRoleNodes
            .Where(n => n.ReferencedConditionId == conditionId)
            .Select(n => new { RuleName = n.OwnerRule!.Name, ConditionName = n.OwnerCondition!.Name })
            .ToListAsync();

        return (
            nodes.Where(n => n.RuleName != null).Select(n => n.RuleName).Distinct().ToList(),
            nodes.Where(n => n.ConditionName != null).Select(n => n.ConditionName).Distinct().ToList());
    }
}
