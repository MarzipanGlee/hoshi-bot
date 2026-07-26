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
        return await ScopeQuery<GuildFeatureSettingSnowflake>(db, guildId, feature, audience, guildAllianceId, key)
            .Select(s => (ulong?)s.Value)
            .FirstOrDefaultAsync();
    }

    public Task SetSnowflakeAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, ulong? value) =>
        SetSingularAsync<GuildFeatureSettingSnowflake>(guildId, feature, audience, guildAllianceId, key, clear: value is null, (db, existing) =>
        {
            // Snowflake collision strategy: keep the row that already holds the target value (if
            // any) and drop the rest, rather than delete-all + insert. EF Core doesn't guarantee
            // the DELETE runs before the ADD within one SaveChanges, and this table's unique index
            // includes Value — so re-adding the same value while its old row is being deleted
            // collides (Postgres 23505; hit for real by the periodic jobs re-setting an unchanged
            // id). Keeping the matching row sidesteps that and makes re-setting an unchanged value
            // a no-op instead of churn. (Update-in-place — the text table's strategy — would be
            // wrong here: retargeting one row onto a value a sibling row already holds collides on
            // the same Value-including index.)
            var target = value!.Value; // non-null here: `clear` above covers the null case
            var match = existing.FirstOrDefault(s => s.Value == target);
            var others = existing.Where(s => s != match).ToList();
            if (others.Count > 0)
                db.GuildFeatureSettingSnowflakes.RemoveRange(others);

            if (match is null)
            {
                var row = NewScopedRow<GuildFeatureSettingSnowflake>(guildId, feature, audience, guildAllianceId, key);
                row.Value = target;
                db.GuildFeatureSettingSnowflakes.Add(row);
                return true;
            }

            return others.Count > 0; // false: already exactly the desired single row — no write needed
        });

    public async Task<List<ulong>> GetSnowflakeListAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await ScopeQuery<GuildFeatureSettingSnowflake>(db, guildId, feature, audience, guildAllianceId, key)
            .Select(s => s.Value)
            .ToListAsync();
    }

    public async Task AddSnowflakeListValueAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, ulong value)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var exists = await ScopeQuery<GuildFeatureSettingSnowflake>(db, guildId, feature, audience, guildAllianceId, key)
            .AnyAsync(s => s.Value == value);
        if (exists)
            return;

        var row = NewScopedRow<GuildFeatureSettingSnowflake>(guildId, feature, audience, guildAllianceId, key);
        row.Value = value;
        db.GuildFeatureSettingSnowflakes.Add(row);
        await db.SaveChangesAsync();
    }

    public async Task RemoveSnowflakeListValueAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, ulong value)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await ScopeQuery<GuildFeatureSettingSnowflake>(db, guildId, feature, audience, guildAllianceId, key)
            .Where(s => s.Value == value)
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
        return await ScopeQuery<GuildFeatureSettingText>(db, guildId, feature, audience, guildAllianceId, key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
    }

    public Task SetTextAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, string? value) =>
        SetSingularAsync<GuildFeatureSettingText>(guildId, feature, audience, guildAllianceId, key, clear: string.IsNullOrEmpty(value), (db, existing) =>
        {
            // Text collision strategy: update the existing row in place instead of delete +
            // re-insert — this table's unique index is on the key columns only (no Value), so
            // re-inserting the same key while its old row is being deleted collides (EF Core
            // doesn't guarantee DELETE-before-ADD within one SaveChanges → Postgres 23505).
            // Updating also makes re-setting an unchanged value a no-op. (The snowflake table's
            // keep-the-matching-row strategy would be wrong here: with a different value there is
            // no matching row, so it degenerates into exactly the delete + re-insert-same-key
            // pattern this index forbids.)
            var primary = existing.FirstOrDefault();
            var extras = existing.Skip(1).ToList();
            if (extras.Count > 0)
                db.GuildFeatureSettingTexts.RemoveRange(extras);

            if (primary is null)
            {
                var row = NewScopedRow<GuildFeatureSettingText>(guildId, feature, audience, guildAllianceId, key);
                row.Value = value!;
                db.GuildFeatureSettingTexts.Add(row);
                return true;
            }

            if (primary.Value != value)
            {
                primary.Value = value!;
                return true;
            }

            return extras.Count > 0; // false: unchanged — no write needed
        });

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

    // The one scope predicate both setting tables share (via IGuildFeatureSetting's common
    // (GuildId, Feature, Audience, GuildAllianceId, Key) columns).
    private static IQueryable<TRow> ScopeQuery<TRow>(HoshiBotDbContext db, ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key)
        where TRow : class, IGuildFeatureSetting =>
        db.Set<TRow>()
            .Where(s => s.GuildId == guildId && s.Feature == feature && s.Audience == audience && s.GuildAllianceId == guildAllianceId && s.Key == key);

    private static TRow NewScopedRow<TRow>(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key)
        where TRow : IGuildFeatureSetting, new() =>
        new()
        {
            GuildId = guildId,
            Feature = feature,
            Audience = audience,
            GuildAllianceId = guildAllianceId,
            Key = key,
        };

    // Shared skeleton for the two singular Set methods: scope-validate, load every row in scope,
    // remove them all when clearing (an unset value), otherwise hand off to `reconcile` and save
    // only if it reports a change. `reconcile` is deliberately a per-table STRATEGY POINT, not
    // shared logic: the two tables carry different unique indexes and therefore need different,
    // individually-battle-tested Postgres-23505 collision workarounds — see the comments inside
    // SetSnowflakeAsync (keep-the-matching-row) and SetTextAsync (update-in-place). Do not unify
    // them.
    private async Task SetSingularAsync<TRow>(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, string key, bool clear, Func<HoshiBotDbContext, List<TRow>, bool> reconcile)
        where TRow : class, IGuildFeatureSetting
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await ScopeQuery<TRow>(db, guildId, feature, audience, guildAllianceId, key).ToListAsync();

        if (clear)
        {
            if (existing.Count == 0)
                return;
            db.Set<TRow>().RemoveRange(existing);
            await db.SaveChangesAsync();
            return;
        }

        if (reconcile(db, existing))
            await db.SaveChangesAsync();
    }
}
