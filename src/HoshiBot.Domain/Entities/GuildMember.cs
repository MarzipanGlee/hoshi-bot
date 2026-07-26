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

    // Which of the member's linked players represents them *here* — the source for nickname
    // sync, rank/ops roles and alliance resolution in this guild. Auto-filled when a link is
    // created or the row is seeded; null falls back to the person's oldest link at read time
    // (PlayerLinkService.GetGuildPrimaryPlayersAsync), so sync never silently stalls.
    public int? PrimaryStfcPlayerId { get; set; }

    public StfcPlayer? PrimaryStfcPlayer { get; set; }
}
