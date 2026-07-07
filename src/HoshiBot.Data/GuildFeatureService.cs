using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Presence of a GuildDisabledFeature row means "off" — see that entity for why absence
// means enabled by default. Used both to gate feature entry points and to filter which
// Command Bridge hub buttons get posted for a guild.
public class GuildFeatureService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<bool> IsEnabledAsync(ulong guildId, GuildFeature feature)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return !await db.GuildDisabledFeatures.AnyAsync(f => f.GuildId == guildId && f.Feature == feature);
    }

    public async Task<HashSet<GuildFeature>> GetDisabledAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.GuildDisabledFeatures.Where(f => f.GuildId == guildId).Select(f => f.Feature).ToListAsync()).ToHashSet();
    }

    public async Task SetEnabledAsync(ulong guildId, GuildFeature feature, bool enabled)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.GuildDisabledFeatures.FirstOrDefaultAsync(f => f.GuildId == guildId && f.Feature == feature);

        if (enabled)
        {
            if (existing is not null)
                db.GuildDisabledFeatures.Remove(existing);
        }
        else if (existing is null)
        {
            db.GuildDisabledFeatures.Add(new GuildDisabledFeature { GuildId = guildId, Feature = feature });
        }

        await db.SaveChangesAsync();
    }

    public static string DisabledMessage(GuildFeature feature) =>
        $"Diese Funktion ({FeatureLabel(feature)}) ist auf diesem Server deaktiviert.";

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
        _ => feature.ToString(),
    };
}
