namespace HoshiBot.Domain.Entities;

// The shared (GuildId, Feature, Audience, GuildAllianceId, Key) scope columns of the two
// per-feature setting tables (GuildFeatureSettingSnowflake / GuildFeatureSettingText), so
// GuildFeatureSettingsService can share its scope-filter and upsert plumbing generically.
// Value is deliberately NOT part of this interface: the two tables' value types differ, and —
// more importantly — their unique indexes (and therefore their Postgres-23505-avoiding upsert
// strategies) differ with them. See GuildFeatureSettingsService.SetSnowflakeAsync/SetTextAsync.
public interface IGuildFeatureSetting
{
    ulong GuildId { get; set; }

    GuildFeature Feature { get; set; }

    GuildAudience Audience { get; set; }

    int? GuildAllianceId { get; set; }

    string Key { get; set; }
}
