using System.Collections.Concurrent;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// In-process cache for resolved languages — these lookups run on every rendered message
// including Quartz per-guild fan-outs. Web writes settings from a different process, so
// invalidation can't be cross-process; a short TTL bounds staleness instead (same
// tradeoff as DiscordGuildDataService's 10-minute guild-data cache).
public class LanguageCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(char Kind, ulong A, int B), (Language Language, DateTimeOffset At)> _entries = new();

    public bool TryGet(char kind, ulong a, int b, out Language language)
    {
        language = default;
        if (!_entries.TryGetValue((kind, a, b), out var entry) || DateTimeOffset.UtcNow - entry.At > Ttl)
            return false;

        language = entry.Language;
        return true;
    }

    public Language Set(char kind, ulong a, int b, Language language)
    {
        _entries[(kind, a, b)] = (language, DateTimeOffset.UtcNow);
        return language;
    }

    public void Invalidate(char kind, ulong a, int b = 0) => _entries.TryRemove((kind, a, b), out _);
}

// Resolves the effective bot language for each scope per docs/localization-plan.md. The
// precedence rules live in the pure LanguagePolicy (Domain); this service only supplies
// the stored values and caches results.
public class LanguageResolver(IDbContextFactory<HoshiBotDbContext> dbFactory, LanguageCache cache)
{
    public async Task<Language> ForGuildAsync(ulong guildId)
    {
        if (cache.TryGet('g', guildId, 0, out var cached))
            return cached;

        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.GuildSettings
            .Where(s => s.GuildId == guildId)
            .Select(s => new { s.Language, s.Guild.PreferredLocale })
            .FirstOrDefaultAsync();

        // No settings row yet: still honor the synced Discord locale if the guild exists.
        var preferredLocale = row?.PreferredLocale
            ?? await db.DiscordGuilds.Where(g => g.Id == guildId).Select(g => g.PreferredLocale).FirstOrDefaultAsync();

        return cache.Set('g', guildId, 0, LanguagePolicy.ForGuild(row?.Language, preferredLocale));
    }

    // Non-Alliance audiences (Server/VeilGroup/Community). Alliance-audience callers use
    // ForAllianceAsync — per-alliance beats per-audience there.
    public async Task<Language> ForAudienceAsync(ulong guildId, GuildAudience audience)
    {
        if (audience == GuildAudience.Alliance)
            throw new ArgumentException("Alliance-audience language is per-alliance — use ForAllianceAsync.", nameof(audience));

        if (cache.TryGet('a', guildId, (int)audience, out var cached))
            return cached;

        await using var db = await dbFactory.CreateDbContextAsync();
        var code = await db.GuildAudienceSettings
            .Where(l => l.GuildId == guildId && l.Audience == audience)
            .Select(l => l.Language)
            .FirstOrDefaultAsync();

        return cache.Set('a', guildId, (int)audience, LanguagePolicy.ForAudience(code, await ForGuildAsync(guildId)));
    }

    public async Task<Language> ForAllianceAsync(int guildAllianceId)
    {
        if (cache.TryGet('l', (ulong)guildAllianceId, 0, out var cached))
            return cached;

        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.GuildAlliances
            .Where(ga => ga.Id == guildAllianceId)
            .Select(ga => new { ga.Language, ga.GuildId })
            .FirstOrDefaultAsync();

        if (row is null)
            return Language.En;

        return cache.Set('l', (ulong)guildAllianceId, 0, LanguagePolicy.ForAlliance(row.Language, await ForGuildAsync(row.GuildId)));
    }

    // interactionUserLocale: pass Interaction.UserLocale when in an interaction — it's the
    // freshest signal and skips a DB read for users without an explicit preference.
    // scopeGuildId: the surrounding guild, used as the final fallback for users the bot
    // knows nothing about (never interacted, no preference).
    public async Task<Language> ForUserAsync(ulong userId, string? interactionUserLocale = null, ulong? scopeGuildId = null)
    {
        string? explicitCode = null;
        string? storedLocale = null;

        if (cache.TryGet('u', userId, 0, out var cachedUser) && interactionUserLocale is null)
            return cachedUser;

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            var row = await db.DiscordUsers
                .Where(u => u.DiscordUserId == userId)
                .Select(u => new { u.Language, u.DiscordLocale })
                .FirstOrDefaultAsync();
            explicitCode = row?.Language;
            storedLocale = row?.DiscordLocale;
        }

        var scopeFallback = scopeGuildId is { } guildId ? await ForGuildAsync(guildId) : Language.En;
        var resolved = LanguagePolicy.ForUser(explicitCode, interactionUserLocale, storedLocale, scopeFallback);

        // Cache only the interaction-free resolution: with a live locale in the chain the
        // result may differ per interaction and must not stick.
        if (interactionUserLocale is null)
            cache.Set('u', userId, 0, resolved);

        return resolved;
    }

    // Records the user's Discord client locale from an interaction (UserLocaleSyncHandler).
    // Write-on-change only; creates the DiscordUser row if the user is unknown so DMs/jobs
    // can resolve their language later.
    public async Task RecordUserLocaleAsync(ulong userId, string userLocale)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.DiscordUsers.FindAsync(userId);
        if (user is null)
        {
            db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId, DiscordLocale = userLocale });
        }
        else if (user.DiscordLocale != userLocale)
        {
            user.DiscordLocale = userLocale;
        }
        else
        {
            return;
        }

        await db.SaveChangesAsync();
        cache.Invalidate('u', userId);
    }
}
