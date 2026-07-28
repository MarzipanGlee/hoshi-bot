using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Presence of a GuildEnabledFeature row means "on" for that guild+audience — absence means
// disabled (the default). Used both to gate feature entry points and to filter which
// Command Bridge hub buttons get posted for a guild.
//
// Single-audience features (7 of 14) have only one possible Audience value
// (GuildFeatureAudiences.SingleAudience) — the guild-wide overloads below are exactly
// equivalent to the audience-explicit ones for those. For the 7 multi-audience features
// (Announcements/Tickets/AnonymousMessaging/ServerStatus/InfiniteIncursions/RankRoles/OpsLevelRoles),
// the guild-wide overloads are a transitional shim ("enabled if ANY relevant audience is on" / "set every
// relevant audience at once") preserving today's one-shared-switch behavior for call sites
// that haven't yet been upgraded to call the audience-explicit overloads directly — see the
// per-audience settings plan's phased build sequence.
public class GuildFeatureService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<bool> IsEnabledAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildEnabledFeatures.AnyAsync(f =>
            f.GuildId == guildId && f.Feature == feature && f.Audience == audience && f.GuildAllianceId == guildAllianceId);
    }

    public async Task<bool> IsEnabledAsync(ulong guildId, GuildFeature feature)
    {
        var relevant = GuildFeatureAudiences.RelevantAudiences(feature);
        var enabled = await GetEnabledAudiencesAsync(guildId, feature);
        return GuildFeatureAudiences.EnumerateFlags(relevant).Any(enabled.Contains);
    }

    // Gate helper for interaction handlers: null when the feature is enabled for this guild, else the
    // localized "disabled" message to return. Collapses the repeated
    // `if (!await IsEnabledAsync(id, X)) return DisabledMessage(X, lang)` guard — and its duplicated
    // feature arg — to `if (await EnsureEnabledAsync(id, X, lang) is { } msg) return <wrap>(msg)`.
    public async Task<string?> EnsureEnabledAsync(ulong guildId, GuildFeature feature, Language lang) =>
        await IsEnabledAsync(guildId, feature) ? null : DisabledMessage(feature, lang);

    // The distinct guilds that have this feature enabled for any audience/alliance — the guild set a
    // feature's periodic job iterates. Replaces the verbatim
    // `db.GuildEnabledFeatures.Where(f => f.Feature == X).Select(f => f.GuildId).Distinct()` that was
    // copy-pasted across ~11 sync/notify jobs.
    public async Task<List<ulong>> GetEnabledGuildIdsAsync(GuildFeature feature, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.GuildEnabledFeatures
            .Where(f => f.Feature == feature)
            .Select(f => f.GuildId)
            .Distinct()
            .ToListAsync(ct);
    }

    // Distinct audiences enabled for this feature (across all alliances for the Alliance
    // audience). Alliance identity is not distinguished here — use GetEnabledAllianceIdsAsync
    // for that; this answers "is the feature on for any scope of this audience?".
    public async Task<HashSet<GuildAudience>> GetEnabledAudiencesAsync(ulong guildId, GuildFeature feature)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId && f.Feature == feature)
            .Select(f => f.Audience)
            .ToListAsync())
            .ToHashSet();
    }

    // The specific linked alliances this feature is enabled for (Audience == Alliance rows).
    public async Task<HashSet<int>> GetEnabledAllianceIdsAsync(ulong guildId, GuildFeature feature)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId && f.Feature == feature && f.Audience == GuildAudience.Alliance && f.GuildAllianceId != null)
            .Select(f => f.GuildAllianceId!.Value)
            .ToListAsync())
            .ToHashSet();
    }

    // Bulk fetch of every enabled (Feature, Audience, GuildAllianceId) tuple for a guild — lets
    // the nav menu and Features page load all enablement in one round-trip instead of a query
    // per feature.
    public async Task<HashSet<(GuildFeature Feature, GuildAudience Audience, int? GuildAllianceId)>> GetEnabledAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId)
            .Select(f => new { f.Feature, f.Audience, f.GuildAllianceId })
            .ToListAsync())
            .Select(f => (f.Feature, f.Audience, f.GuildAllianceId))
            .ToHashSet();
    }

    // Guild-wide bulk fetch — which of the 12 GuildFeature values are OFF for this guild
    // (i.e. not enabled for any of their relevant audiences). Transitional shim for
    // call sites not yet upgraded to be audience-aware (e.g. CommandBridge hub filtering
    // before its own phased pass); mirrors the old GuildDisabledFeature-backed method this
    // replaces.
    public async Task<HashSet<GuildFeature>> GetDisabledAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var enabledByFeature = (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId)
            .Select(f => new { f.Feature, f.Audience })
            .ToListAsync())
            .GroupBy(f => f.Feature)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Audience).ToHashSet());

        var disabled = new HashSet<GuildFeature>();
        foreach (var feature in Enum.GetValues<GuildFeature>())
        {
            var relevant = GuildFeatureAudiences.RelevantAudiences(feature);
            var enabledAudiences = enabledByFeature.GetValueOrDefault(feature, []);
            if (!GuildFeatureAudiences.EnumerateFlags(relevant).Any(enabledAudiences.Contains))
                disabled.Add(feature);
        }

        return disabled;
    }

    public async Task SetEnabledAsync(ulong guildId, GuildFeature feature, GuildAudience audience, int? guildAllianceId, bool enabled)
    {
        FeatureScopeGuard.Validate(audience, guildAllianceId);
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildEnabledFeatures.FirstOrDefaultAsync(f =>
            f.GuildId == guildId && f.Feature == feature && f.Audience == audience && f.GuildAllianceId == guildAllianceId);

        if (enabled)
        {
            if (existing is null)
                db.GuildEnabledFeatures.Add(new GuildEnabledFeature { GuildId = guildId, Feature = feature, Audience = audience, GuildAllianceId = guildAllianceId });
        }
        else if (existing is not null)
        {
            db.GuildEnabledFeatures.Remove(existing);
        }

        await db.SaveChangesAsync();
    }

    // Guild-wide shim: set the feature on/off across every relevant audience at once. For the
    // Alliance audience this fans out to every linked alliance (each needs its own row); other
    // audiences are guild-scoped (null). Used by the setup wizard / bulk-toggle paths that
    // don't target a single alliance.
    public async Task SetEnabledAsync(ulong guildId, GuildFeature feature, bool enabled)
    {
        var relevant = GuildFeatureAudiences.RelevantAudiences(feature);
        foreach (var audience in GuildFeatureAudiences.EnumerateFlags(relevant))
        {
            if (audience == GuildAudience.Alliance)
            {
                await using var db = await dbFactory.CreateDbContextAsync();
                var allianceIds = await db.GuildAlliances
                    .Where(ga => ga.GuildId == guildId)
                    .Select(ga => ga.Id)
                    .ToListAsync();
                foreach (var allianceId in allianceIds)
                    await SetEnabledAsync(guildId, feature, audience, allianceId, enabled);
            }
            else
            {
                await SetEnabledAsync(guildId, feature, audience, null, enabled);
            }
        }
    }

    // Label/message texts live in the message catalog ("Feature.<X>" / "Audience.<X>" /
    // "Feature.Disabled" keys); these delegations keep the historical call-site shape.
    public static string DisabledMessage(GuildFeature feature, Language lang) =>
        Msg.Feature.Disabled(lang, feature);

    public static string AudienceLabel(GuildAudience audience, Language lang) =>
        Msg.Audience.Label(lang, audience);

    public static string FeatureLabel(GuildFeature feature, Language lang) =>
        Msg.Feature.Label(lang, feature);
}
