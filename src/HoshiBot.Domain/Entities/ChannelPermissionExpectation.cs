namespace HoshiBot.Domain.Entities;

// The exact Allow/Deny permission overwrite a specific role should have on a specific
// channel. Both bitmasks are checked for an exact match against Discord's actual overwrite,
// not just "at least these bits" — an unexpected extra permission is flagged just like a
// missing expected one (ported from the YAGPDB-era check-channel-permissions command).
public class ChannelPermissionExpectation
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ChannelId { get; set; }

    public ulong RoleId { get; set; }

    public ulong Allow { get; set; }

    public ulong Deny { get; set; }
}
