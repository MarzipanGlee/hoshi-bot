using HoshiBot.Domain.Entities;
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
    public async Task<bool> IsEnabledAsync(ulong guildId, GuildFeature feature, GuildAudience audience)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildEnabledFeatures.AnyAsync(f => f.GuildId == guildId && f.Feature == feature && f.Audience == audience);
    }

    public async Task<bool> IsEnabledAsync(ulong guildId, GuildFeature feature)
    {
        var relevant = GuildFeatureAudiences.RelevantAudiences(feature);
        var enabled = await GetEnabledAudiencesAsync(guildId, feature);
        return GuildFeatureAudiences.EnumerateFlags(relevant).Any(enabled.Contains);
    }

    public async Task<HashSet<GuildAudience>> GetEnabledAudiencesAsync(ulong guildId, GuildFeature feature)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId && f.Feature == feature)
            .Select(f => f.Audience)
            .ToListAsync())
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

    public async Task SetEnabledAsync(ulong guildId, GuildFeature feature, GuildAudience audience, bool enabled)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildEnabledFeatures.FirstOrDefaultAsync(f => f.GuildId == guildId && f.Feature == feature && f.Audience == audience);

        if (enabled)
        {
            if (existing is null)
                db.GuildEnabledFeatures.Add(new GuildEnabledFeature { GuildId = guildId, Feature = feature, Audience = audience });
        }
        else if (existing is not null)
        {
            db.GuildEnabledFeatures.Remove(existing);
        }

        await db.SaveChangesAsync();
    }

    public async Task SetEnabledAsync(ulong guildId, GuildFeature feature, bool enabled)
    {
        var relevant = GuildFeatureAudiences.RelevantAudiences(feature);
        foreach (var audience in GuildFeatureAudiences.EnumerateFlags(relevant))
            await SetEnabledAsync(guildId, feature, audience, enabled);
    }

    public static string DisabledMessage(GuildFeature feature) =>
        $"Diese Funktion ({FeatureLabel(feature)}) ist auf diesem Server deaktiviert.";

    public static string AudienceLabel(GuildAudience audience) => audience switch
    {
        GuildAudience.Alliance => "Allianz",
        GuildAudience.Server => "Server",
        GuildAudience.VeilGroup => "Veil-Gruppe",
        GuildAudience.Community => "Community",
        _ => audience.ToString(),
    };

    public static string FeatureLabel(GuildFeature feature) => feature switch
    {
        GuildFeature.RaidAlerts => "Raid melden",
        GuildFeature.ShieldReminders => "Schilderinnerung",
        GuildFeature.TerritoryCapture => "Gebietsübernahmen",
        GuildFeature.Announcements => "Ankündigungen",
        GuildFeature.Tickets => "Ticket öffnen",
        GuildFeature.AnonymousMessaging => "Anonyme Nachricht",
        GuildFeature.RoeViolationReports => "ROE Verstoss melden",
        GuildFeature.Absences => "Abwesenheiten verwalten",
        GuildFeature.AlertsOptIn => "Alarme verwalten",
        GuildFeature.Diplomacy => "Diplomatie",
        GuildFeature.ServerStatus => "Serverstatus",
        GuildFeature.InfiniteIncursions => "Infinite-Incursions-Ankündigungen",
        GuildFeature.RankRoles => "Rangrollen",
        GuildFeature.OpsLevelRoles => "Ops-Level-Rollen",
        _ => feature.ToString(),
    };
}
