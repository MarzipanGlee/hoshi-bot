namespace HoshiBot.Domain.Entities;

// Bot-wide administrators — separate from GuildAdminRole (per-guild role-based access).
// Grants access to shared/global game-data management (the STFC catalog) that isn't
// scoped to any single guild. No FK to DiscordUser: a global admin may never have run a
// slash command that upserts one, same reasoning GuildAdminRole.DiscordRoleId has no FK.
public class GlobalAdmin
{
    public ulong DiscordUserId { get; set; }
}
