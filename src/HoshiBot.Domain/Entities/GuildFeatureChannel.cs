namespace HoshiBot.Domain.Entities;

// A channel a feature posts to, as a per-(guild, feature, audience) list — the "separate the
// channel from the role" counterpart to GuildAlertChannel (which couples a channel to one role).
// Used where a feature fans out to several channels but the role to ping is decided elsewhere
// (ClientRelease pings a per-platform, guild-wide role, not a per-channel one). Audience-scoped,
// no GuildAllianceId: like GuildAlertChannel and unlike the per-alliance feature settings, these
// channels are shared across a guild's alliances.
public class GuildFeatureChannel
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildFeature Feature { get; set; }

    public GuildAudience Audience { get; set; }

    public ulong ChannelId { get; set; }
}
