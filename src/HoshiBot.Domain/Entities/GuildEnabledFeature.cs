namespace HoshiBot.Domain.Entities;

// Presence of a row means the feature is turned ON for that guild+audience — absence means
// disabled (the default). Replaces GuildDisabledFeature's opposite convention: a brand-new
// guild, or a feature added in the future, now starts fully off until an admin explicitly
// opts in from the Features page, rather than everything on until someone notices and turns
// it off. Keyed by (GuildId, Feature, Audience, GuildAllianceId) — Audience is always a single
// flag; for single-audience features that's always that feature's one relevant audience (see
// GuildFeatureAudiences). GuildAllianceId scopes the Alliance audience to one specific linked
// alliance (see the field's comment).
public class GuildEnabledFeature
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildFeature Feature { get; set; }

    public GuildAudience Audience { get; set; }

    // Which specific linked alliance this row belongs to, for the Alliance audience. Invariant
    // (guarded in GuildFeatureService): non-null exactly when Audience == GuildAudience.Alliance,
    // null for every other audience. Lets a coalition guild enable a feature for one alliance
    // but not another. Cascade-deleted with its GuildAlliance.
    public int? GuildAllianceId { get; set; }

    public GuildAlliance? GuildAlliance { get; set; }
}
