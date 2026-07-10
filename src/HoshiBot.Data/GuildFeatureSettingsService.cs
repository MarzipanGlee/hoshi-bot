using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Generic per-feature settings storage, keyed by (GuildId, Feature, Audience, Key) —
// replaces the many flat nullable-ulong/string columns that used to live directly on
// GuildSettings. Each feature's own editor/consumer code owns its Key string constants
// locally (Feature already namespaces them, so no shared global key registry is needed).
//
// Whether a Key is meant to hold one value or a list of values is a fact the feature's own
// code knows — call the singular Get/Set methods for a one-per-key setting (e.g. "the
// Tickets channel"), or the list methods for a many-per-key setting. Singularity for
// singular keys is enforced here (upsert-by-replace), not by a DB-level distinction between
// "this key is a list" and "this key is singular" — see GuildFeatureSettingSnowflake's doc
// comment.
public class GuildFeatureSettingsService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<ulong?> GetSnowflakeAsync(ulong guildId, GuildFeature feature, GuildAudience audience, string key)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.Key == key)
            .Select(s => (ulong?)s.Value)
            .FirstOrDefaultAsync();
    }

    public async Task SetSnowflakeAsync(ulong guildId, GuildFeature feature, GuildAudience audience, string key, ulong? value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.Key == key)
            .ToListAsync();
        db.GuildFeatureSettingSnowflakes.RemoveRange(existing);

        if (value is not null)
        {
            db.GuildFeatureSettingSnowflakes.Add(new GuildFeatureSettingSnowflake
            {
                GuildId = guildId,
                Feature = feature,
                Audience = audience,
                Key = key,
                Value = value.Value,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<ulong>> GetSnowflakeListAsync(ulong guildId, GuildFeature feature, GuildAudience audience, string key)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.Key == key)
            .Select(s => s.Value)
            .ToListAsync();
    }

    public async Task AddSnowflakeListValueAsync(ulong guildId, GuildFeature feature, GuildAudience audience, string key, ulong value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var exists = await db.GuildFeatureSettingSnowflakes
            .AnyAsync(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.Key == key && s.Value == value);
        if (exists)
            return;

        db.GuildFeatureSettingSnowflakes.Add(new GuildFeatureSettingSnowflake
        {
            GuildId = guildId,
            Feature = feature,
            Audience = audience,
            Key = key,
            Value = value,
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveSnowflakeListValueAsync(ulong guildId, GuildFeature feature, GuildAudience audience, string key, ulong value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.Key == key && s.Value == value)
            .ToListAsync();
        db.GuildFeatureSettingSnowflakes.RemoveRange(existing);
        await db.SaveChangesAsync();
    }

    // Reverse lookup: which audience(s) have `key` set to `value` for this guild+feature.
    // Used to resolve which audience a trigger belongs to from context alone (e.g. which
    // audience's draft channel a message was posted in) — 1 result is the unambiguous
    // common case, 0 or 2+ means the caller needs another way to disambiguate.
    public async Task<List<GuildAudience>> FindAudiencesByValueAsync(ulong guildId, GuildFeature feature, string key, ulong value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Key == key && s.Value == value)
            .Select(s => s.Audience)
            .ToListAsync();
    }

    public async Task<string?> GetTextAsync(ulong guildId, GuildFeature feature, GuildAudience audience, string key)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingTexts
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
    }

    public async Task SetTextAsync(ulong guildId, GuildFeature feature, GuildAudience audience, string key, string? value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildFeatureSettingTexts
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.Key == key)
            .ToListAsync();
        db.GuildFeatureSettingTexts.RemoveRange(existing);

        if (!string.IsNullOrEmpty(value))
        {
            db.GuildFeatureSettingTexts.Add(new GuildFeatureSettingText
            {
                GuildId = guildId,
                Feature = feature,
                Audience = audience,
                Key = key,
                Value = value,
            });
        }

        await db.SaveChangesAsync();
    }
}
