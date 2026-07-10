namespace HoshiBot.Domain.Entities;

// Presence of a row means the feature is turned ON for that guild+audience — absence means
// disabled (the default). Replaces GuildDisabledFeature's opposite convention: a brand-new
// guild, or a feature added in the future, now starts fully off until an admin explicitly
// opts in from the Dashboard, rather than everything on until someone notices and turns it
// off. Keyed by (GuildId, Feature, Audience) — Audience is always a single flag; for
// single-audience features that's always that feature's one relevant audience (see
// GuildFeatureAudiences).
public class GuildEnabledFeature
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildFeature Feature { get; set; }

    public GuildAudience Audience { get; set; }
}
