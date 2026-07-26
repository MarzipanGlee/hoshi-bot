using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Discord.Scheduling;

// Announces a newly-scheduled Alliance Tournament event as advance warning. Not known to be
// region-specific (unlike Infinite Incursions) — every guild tracking any server (via
// GuildServer) is notified, since there's currently no finer signal to filter on. Revisit if
// that assumption turns out to be wrong (region-specific tournament start times have not yet
// been confirmed from a real announcement post). Same one-time-seed situation as
// ServerStatusNotifyJob (see there for why).
public class AllianceTournamentNotifyJob(
    HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding)
    : DiffNotifyJobBase<StfcEventStatus>(db, dispatcher, embedBranding)
{
    // The string value ("alliance_tournaments") is a real persisted lookup key
    // (StfcEventStatus.EventGroup) — do not change the value, only the constant's own name
    // is cosmetic.
    private const string AllianceTournamentEventGroup = "alliance_tournaments";

    protected override GuildAlertChannelKind ChannelKind => GuildAlertChannelKind.AllianceTournament;
    protected override GuildFeature Feature => GuildFeature.AllianceTournament;

    protected override async Task<List<StfcEventStatus>> LoadPendingRowsAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var tournament = await Db.StfcEventStatuses.FirstOrDefaultAsync(e => e.EventGroup == AllianceTournamentEventGroup);
        if (tournament is null || tournament.EventStart == tournament.NotifiedEventStart || tournament.EventStart <= now)
            return [];

        return [tournament];
    }

    protected override Task<List<ulong>> ResolveGuildIdsAsync(StfcEventStatus row) =>
        Db.GuildServers.Select(g => g.GuildId).Distinct().ToListAsync();

    protected override (string Content, NetCord.Color Color) BuildAnnouncement(StfcEventStatus row) =>
        ($"🏆 A new Alliance Tournament is scheduled to start <t:{row.EventStart.ToUnixTimeSeconds()}:R>!",
            EmbedBranding.WarningColor);

    protected override void MarkNotified(StfcEventStatus row) => row.NotifiedEventStart = row.EventStart;
}
