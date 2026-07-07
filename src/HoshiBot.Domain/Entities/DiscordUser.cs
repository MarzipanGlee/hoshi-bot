namespace HoshiBot.Domain.Entities;

// Global, not guild-scoped — a person's linked STFC player(s) are a fact about
// them, not about one guild. Per-guild membership facts live on GuildMember.
public class DiscordUser
{
    public ulong DiscordUserId { get; set; }

    public ICollection<UserPlayer> PlayerLinks { get; set; } = [];

    public ICollection<GuildMember> GuildMemberships { get; set; } = [];
}
