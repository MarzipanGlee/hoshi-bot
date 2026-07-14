namespace HoshiBot.Domain.Entities;

// A single ulong-valued per-feature setting (a channel ID or role ID) — replaces the many
// flat nullable-ulong columns that used to live directly on GuildSettings. Keyed by
// (GuildId, Feature, Audience, Key): Audience is always a single flag, never combined bits
// (single-audience features always use their one relevant audience; multi-audience
// features get one independent row set per relevant audience). Whether a given Key is
// meant to hold one value or a list of values is a fact the feature's own code knows, not
// something stored here — see GuildFeatureSettingsService for the singular vs. list API.
public class GuildFeatureSettingSnowflake
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildFeature Feature { get; set; }

    public GuildAudience Audience { get; set; }

    // Scopes the Alliance audience to one specific linked alliance. Invariant (guarded in
    // GuildFeatureSettingsService): non-null exactly when Audience == GuildAudience.Alliance,
    // null otherwise. Cascade-deleted with its GuildAlliance.
    public int? GuildAllianceId { get; set; }

    public GuildAlliance? GuildAlliance { get; set; }

    public string Key { get; set; } = "";

    public ulong Value { get; set; }
}
