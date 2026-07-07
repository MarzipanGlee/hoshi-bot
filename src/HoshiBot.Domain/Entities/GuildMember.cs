namespace HoshiBot.Domain.Entities;

// Replaces the old Member entity's guild-scoping role; per-guild membership facts
// live here, identity/player-linking lives on User.
public class GuildMember
{
    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong DiscordUserId { get; set; }

    public DiscordUser User { get; set; } = null!;

    public DateTimeOffset JoinedAt { get; set; }
}
