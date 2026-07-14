namespace HoshiBot.Domain.Entities;

// Free-text per-feature setting counterpart to GuildFeatureSettingSnowflake — same
// (GuildId, Feature, Audience, Key) shape, but for the one setting that isn't a Discord
// snowflake (TerritoryCapture's instructions text). Always singular in practice today; see
// GuildFeatureSettingsService.
public class GuildFeatureSettingText
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

    public string Value { get; set; } = "";
}
