using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Generic per-feature settings storage, keyed by (GuildId, Feature, Audience, GuildAllianceId,
// Key) — replaces the many flat nullable-ulong/string columns that used to live directly on
// GuildSettings. Each feature's own editor/consumer code owns its Key string constants locally
// (Feature already namespaces them, so no shared global key registry is needed).
//
// GuildAllianceId scopes the Alliance audience to one specific linked alliance (null for every
// other audience — see FeatureScopeGuard). A coalition guild that links two alliances gets an
// independent settings bucket per alliance.
//
// Whether a Key is meant to hold one value or a list of values is a fact the feature's own
// code knows — call the singular Get/Set methods for a one-per-key setting (e.g. "the Tickets
// channel"), or the list methods for a many-per-key setting. Singularity for singular keys is
// enforced here (upsert-by-replace), not by a DB-level distinction between "this key is a list"
// and "this key is singular" — see GuildFeatureSettingSnowflake's doc comment.
public class GuildFeatureSettingsService(IDbContextFactory<HoshiBotDbContext> dbFactory, SettingSecretProtector secretProtector)
{
    public async Task<ulong?> GetSnowflakeAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key)
            .Select(s => (ulong?)s.Value)
            .FirstOrDefaultAsync();
    }

    public async Task SetSnowflakeAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, ulong? value)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key)
            .ToListAsync();

        if (value is null)
        {
            if (existing.Count == 0)
                return;
            db.GuildFeatureSettingSnowflakes.RemoveRange(existing);
            await db.SaveChangesAsync();
            return;
        }

        // Keep the row that already holds the target value (if any) and drop the rest, rather than
        // delete-all + insert. EF Core doesn't guarantee the DELETE runs before the ADD within one
        // SaveChanges, and this table's unique index includes Value — so re-adding the same value
        // while its old row is being deleted collides (Postgres 23505; hit for real by the periodic
        // jobs re-setting an unchanged id). Keeping the matching row sidesteps that and makes
        // re-setting an unchanged value a no-op instead of churn.
        var match = existing.FirstOrDefault(s => s.Value == value.Value);
        var others = existing.Where(s => s != match).ToList();
        if (others.Count > 0)
            db.GuildFeatureSettingSnowflakes.RemoveRange(others);

        if (match is null)
        {
            db.GuildFeatureSettingSnowflakes.Add(new GuildFeatureSettingSnowflake
            {
                GuildId = guildId,
                Feature = feature,
                Audience = audience,
                GuildAllianceId = guildAllianceId,
                Key = key,
                Value = value.Value,
            });
        }
        else if (others.Count == 0)
        {
            return; // already exactly the desired single row — no write needed
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<ulong>> GetSnowflakeListAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key)
            .Select(s => s.Value)
            .ToListAsync();
    }

    public async Task AddSnowflakeListValueAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, ulong value)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var exists = await db.GuildFeatureSettingSnowflakes
            .AnyAsync(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key && s.Value == value);
        if (exists)
            return;

        db.GuildFeatureSettingSnowflakes.Add(new GuildFeatureSettingSnowflake
        {
            GuildId = guildId,
            Feature = feature,
            Audience = audience,
            GuildAllianceId = guildAllianceId,
            Key = key,
            Value = value,
        });
        await db.SaveChangesAsync();
    }

    public async Task RemoveSnowflakeListValueAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, ulong value)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key && s.Value == value)
            .ToListAsync();
        db.GuildFeatureSettingSnowflakes.RemoveRange(existing);
        await db.SaveChangesAsync();
    }

    // Reverse lookup: which (audience, alliance) scope(s) have `key` set to `value` for this
    // guild+feature. Used to resolve which scope a trigger belongs to from context alone (e.g.
    // which audience's/alliance's draft channel a message was posted in) — 1 result is the
    // unambiguous common case, 0 or 2+ means the caller needs another way to disambiguate.
    public async Task<List<(GuildAudience Audience, int? GuildAllianceId)>> FindScopesByValueAsync(ulong guildId, GuildFeature feature, string key, ulong value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Key == key && s.Value == value)
            .Select(s => new { s.Audience, s.GuildAllianceId })
            .ToListAsync())
            .Select(s => (s.Audience, s.GuildAllianceId))
            .ToList();
    }

    public async Task<string?> GetTextAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildFeatureSettingTexts
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
    }

    public async Task SetTextAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, string? value)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildFeatureSettingTexts
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key)
            .ToListAsync();

        if (string.IsNullOrEmpty(value))
        {
            if (existing.Count == 0)
                return;
            db.GuildFeatureSettingTexts.RemoveRange(existing);
            await db.SaveChangesAsync();
            return;
        }

        // Update the existing row in place instead of delete + re-insert: this table's unique index
        // is on the key columns, so re-inserting the same key while its old row is being deleted
        // collides (EF Core doesn't guarantee DELETE-before-ADD within one SaveChanges → Postgres
        // 23505). Updating also makes re-setting an unchanged value a no-op.
        var primary = existing.FirstOrDefault();
        var extras = existing.Skip(1).ToList();
        if (extras.Count > 0)
            db.GuildFeatureSettingTexts.RemoveRange(extras);

        if (primary is null)
        {
            db.GuildFeatureSettingTexts.Add(new GuildFeatureSettingText
            {
                GuildId = guildId,
                Feature = feature,
                Audience = audience,
                GuildAllianceId = guildAllianceId,
                Key = key,
                Value = value,
            });
        }
        else if (primary.Value != value)
        {
            primary.Value = value;
        }
        else if (extras.Count == 0)
        {
            return; // unchanged — no write needed
        }

        await db.SaveChangesAsync();
    }

    // Secret-typed text setting (e.g. a third-party API key): same (Feature, Audience, Key) storage
    // as GetText/SetText, but the value is transparently encrypted at rest via SettingSecretProtector
    // so callers still pass/receive a plain string. See docs/backlog.md "Encrypt per-guild secrets".
    public async Task<string?> GetSecretAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key)
    {
        var stored = await GetTextAsync(guildId, feature, audience, guildAllianceId, key);
        if (string.IsNullOrEmpty(stored))
            return stored;

        // Upgrade a legacy plaintext value (stored before encryption existed) to ciphertext on first
        // read once a key is configured, so it doesn't stay in the clear forever. One-time write:
        // subsequent reads see the prefix and skip straight to decrypt.
        if (secretProtector.IsConfigured && !SettingSecretProtector.IsProtected(stored))
        {
            await SetTextAsync(guildId, feature, audience, guildAllianceId, key, secretProtector.Protect(stored));
            return stored;
        }

        return secretProtector.Unprotect(stored);
    }

    public Task SetSecretAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, string? value) =>
        SetTextAsync(guildId, feature, audience, guildAllianceId, key,
            string.IsNullOrEmpty(value) ? null : secretProtector.Protect(value));
}
