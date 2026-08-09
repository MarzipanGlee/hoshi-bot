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

    // When this row was created, i.e. when the feature was switched on for this scope. Boarding uses
    // it as the "only members who joined after we turned this on" cutoff.
    //
    // Here rather than in a feature's settings because the row is written from more than one place
    // (the feature editor's switch and the Features index page's inline toggle both go through
    // GuildFeatureService.SetEnabledAsync). A settings key stamped by one of them would be null for
    // a guild that used the other, and a null cutoff means either "board nobody" or "board
    // everybody" — neither of which anyone asked for.
    //
    // Disable then re-enable resets it, because the row is deleted and recreated. That is the right
    // meaning: turning it back on means start again.
    //
    // Nullable because rows written before this column existed genuinely do not know when they were
    // enabled, and a fabricated 0001-01-01 would read as a real cutoff that boards the entire guild.
    // Boarding rows always have a value — the feature postdates the column — so its own logic can
    // treat null as "no cutoff recorded" and board nobody rather than everybody.
    public DateTimeOffset? EnabledAt { get; set; }
}
