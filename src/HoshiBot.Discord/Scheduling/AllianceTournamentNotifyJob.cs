using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Announces a newly-scheduled Alliance Tournament event as advance warning. Not known to be
// region-specific (unlike Infinite Incursions) — every guild tracking any server (via
// GuildServer) is notified, since there's currently no finer signal to filter on. Revisit if
// that assumption turns out to be wrong (region-specific tournament start times have not yet
// been confirmed from a real announcement post). Same one-time-seed situation as
// ServerStatusNotifyJob (see there for why).
public class AllianceTournamentNotifyJob(
    HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding) : IJob
{
    // The string value ("alliance_tournaments") is a real persisted lookup key
    // (StfcEventStatus.EventGroup) — do not change the value, only the constant's own name
    // is cosmetic.
    private const string AllianceTournamentEventGroup = "alliance_tournaments";

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTimeOffset.UtcNow;

        var tournament = await db.StfcEventStatuses.FirstOrDefaultAsync(e => e.EventGroup == AllianceTournamentEventGroup);
        if (tournament is null)
            return;

        if (tournament.EventStart == tournament.NotifiedEventStart || tournament.EventStart <= now)
            return;

        var guildIds = await db.GuildServers.Select(g => g.GuildId).Distinct().ToListAsync();

        var content = $"🏆 A new Alliance Tournament is scheduled to start <t:{tournament.EventStart.ToUnixTimeSeconds()}:R>!";

        foreach (var guildId in guildIds)
        {
            var embed = new EmbedProperties
            {
                Description = content,
                Color = EmbedBranding.WarningColor,
                Author = await embedBranding.BuildAuthorAsync(guildId),
                Footer = embedBranding.BuildFooter(guildId),
            };
            await dispatcher.SendPublicToEnabledAudiencesAsync(
                guildId, GuildAlertChannelKind.AllianceTournament, GuildFeature.AllianceTournament, content, embed: embed);
        }

        tournament.NotifiedEventStart = tournament.EventStart;
        await db.SaveChangesAsync();
    }
}
