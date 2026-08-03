namespace HoshiBot.Domain.Entities;

// A named, reusable condition tree — the thing that lets "one of our alliance tags" be built once
// and then used with AND in one rule and with NOT in another, instead of being duplicated and
// drifting apart. It has no target role of its own; it only ever means something when a rule (or
// another condition) references it via a ConditionNodeKind.MatchesCondition node.
public class ConditionalRoleCondition
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // Shown in the picker wherever a condition can be referenced, so it has to read on its own.
    public required string Name { get; set; }

    // The nodes making up THIS condition's own tree (OwnerConditionId), not the nodes referencing it.
    public ICollection<ConditionalRoleNode> Nodes { get; set; } = [];
}
