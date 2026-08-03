namespace HoshiBot.Domain.Entities;

// One admin-authored rule of the Conditional Roles feature: grant TargetRoleId to every member whose
// roles satisfy the condition tree hanging off this rule (ConditionalRoleNode.OwnerRuleId), and take
// it away from everyone else. Guild-wide, like the other role-sync features.
//
// Several rules may target the same role — a member holds it if ANY of them matches, so the target
// is not unique here.
public class ConditionalRoleRule
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    // Admin-facing label, shown in the rules list. Not used for matching.
    public required string Name { get; set; }

    public ulong TargetRoleId { get; set; }

    // Off keeps the rule and its tree but takes it out of the sync entirely — its target role is no
    // longer managed by it, so members keep whatever they hold rather than having it stripped.
    public bool Enabled { get; set; } = true;

    public ICollection<ConditionalRoleNode> Nodes { get; set; } = [];
}
