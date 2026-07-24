using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Web.Components.Pages.Manage.Guild.Features;

// A one-shot in-memory snapshot of a guild's feature settings / enablement / channels, loaded once
// (5 bulk queries). The Features page passes it into FeatureModuleContext so the per-feature
// IsConfiguredAsync + dependency checks read memory instead of firing ~40-100 single-row queries
// (each opening its own DbContext). Single-feature callers (FeatureSettings.razor, the toggle
// handler) pass no snapshot and fall back to the live services.
public sealed class FeatureSettingsSnapshot
{
    private readonly Dictionary<(GuildFeature, GuildAudience, int?, string), List<ulong>> _snowflakes;
    private readonly Dictionary<(GuildFeature, GuildAudience, int?, string), string> _texts;
    private readonly HashSet<(GuildFeature, GuildAudience, int?)> _enabled;
    private readonly HashSet<(GuildAlertChannelKind, GuildAudience)> _alertChannels;
    private readonly HashSet<(GuildFeature, GuildAudience)> _featureChannels;

    private FeatureSettingsSnapshot(
        Dictionary<(GuildFeature, GuildAudience, int?, string), List<ulong>> snowflakes,
        Dictionary<(GuildFeature, GuildAudience, int?, string), string> texts,
        HashSet<(GuildFeature, GuildAudience, int?)> enabled,
        HashSet<(GuildAlertChannelKind, GuildAudience)> alertChannels,
        HashSet<(GuildFeature, GuildAudience)> featureChannels)
    {
        _snowflakes = snowflakes;
        _texts = texts;
        _enabled = enabled;
        _alertChannels = alertChannels;
        _featureChannels = featureChannels;
    }

    public static async Task<FeatureSettingsSnapshot> LoadAsync(IDbContextFactory<HoshiBotDbContext> dbFactory, ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var snowflakes = (await db.GuildFeatureSettingSnowflakes.AsNoTracking()
                .Where(s => s.GuildId == guildId)
                .Select(s => new { s.Feature, s.Audience, s.GuildAllianceId, s.Key, s.Value })
                .ToListAsync())
            .GroupBy(s => (s.Feature, s.Audience, s.GuildAllianceId, s.Key))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Value).ToList());

        // Text keys are singular (unique index on the key columns), so ToDictionary is safe.
        var texts = (await db.GuildFeatureSettingTexts.AsNoTracking()
                .Where(s => s.GuildId == guildId)
                .Select(s => new { s.Feature, s.Audience, s.GuildAllianceId, s.Key, s.Value })
                .ToListAsync())
            .ToDictionary(s => (s.Feature, s.Audience, s.GuildAllianceId, s.Key), s => s.Value);

        var enabled = (await db.GuildEnabledFeatures.AsNoTracking()
                .Where(f => f.GuildId == guildId)
                .Select(f => new { f.Feature, f.Audience, f.GuildAllianceId })
                .ToListAsync())
            .Select(f => (f.Feature, f.Audience, f.GuildAllianceId))
            .ToHashSet();

        var alertChannels = (await db.GuildAlertChannels.AsNoTracking()
                .Where(c => c.GuildId == guildId)
                .Select(c => new { c.Kind, c.Audience })
                .ToListAsync())
            .Select(c => (c.Kind, c.Audience))
            .ToHashSet();

        var featureChannels = (await db.GuildFeatureChannels.AsNoTracking()
                .Where(c => c.GuildId == guildId)
                .Select(c => new { c.Feature, c.Audience })
                .ToListAsync())
            .Select(c => (c.Feature, c.Audience))
            .ToHashSet();

        return new FeatureSettingsSnapshot(snowflakes, texts, enabled, alertChannels, featureChannels);
    }

    // Mirrors GuildFeatureSettingsService.GetSnowflakeAsync's singular read (first value for the key).
    public ulong? GetSnowflake(GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key) =>
        _snowflakes.TryGetValue((feature, audience, guildAllianceId, key), out var list) && list.Count > 0
            ? list[0]
            : null;

    public string? GetText(GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key) =>
        _texts.GetValueOrDefault((feature, audience, guildAllianceId, key));

    public bool IsEnabled(GuildFeature feature, GuildAudience audience, int? guildAllianceId) =>
        _enabled.Contains((feature, audience, guildAllianceId));

    public HashSet<GuildAudience> GetEnabledAudiences(GuildFeature feature) =>
        _enabled.Where(e => e.Item1 == feature).Select(e => e.Item2).ToHashSet();

    public HashSet<int> GetEnabledAllianceIds(GuildFeature feature) =>
        _enabled.Where(e => e.Item1 == feature && e.Item2 == GuildAudience.Alliance && e.Item3 is not null)
            .Select(e => e.Item3!.Value)
            .ToHashSet();

    public bool HasAlertChannel(GuildAlertChannelKind kind, GuildAudience audience) =>
        _alertChannels.Contains((kind, audience));

    public bool HasFeatureChannel(GuildFeature feature, GuildAudience audience) =>
        _featureChannels.Contains((feature, audience));
}
