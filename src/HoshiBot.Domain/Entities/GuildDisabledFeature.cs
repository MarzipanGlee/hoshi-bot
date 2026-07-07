namespace HoshiBot.Domain.Entities;

// Presence of a row means the feature is turned OFF for that guild — absence means
// enabled (the default), so every existing guild keeps today's behavior with no rows at
// all, and disabling something is an explicit, additive action rather than needing every
// feature pre-seeded as "enabled=true".
public class GuildDisabledFeature
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildFeature Feature { get; set; }
}
