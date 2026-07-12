using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Announces a newly-scheduled Infinite Incursions event as advance warning. /api/events has no
// server/region field, so Infinite Incursions aren't scoped to a specific StfcServer the way
// StfcServerStatus is — every guild tracking any server (via GuildServer) is notified,
// since there's currently no finer signal to filter on. Revisit once api.stfc.pro
// clarifies whether Infinite Incursions is global or per-region. Same one-time-seed situation
// as ServerStatusNotifyJob (see there for why).
public class InfiniteIncursionsNotifyJob(
    HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding) : IJob
{
    // The string value ("incursions") is a real persisted lookup key (StfcEventStatus.EventGroup
    // is a string PK, seeded with this exact value) — do not change the value, only the constant's
    // own name is cosmetic.
    private const string InfiniteIncursionsEventGroup = "incursions";

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTimeOffset.UtcNow;

        var incursion = await db.StfcEventStatuses.FirstOrDefaultAsync(e => e.EventGroup == InfiniteIncursionsEventGroup);
        if (incursion is null)
            return;

        if (incursion.EventStart == incursion.NotifiedEventStart || incursion.EventStart <= now)
            return;

        var guildIds = await db.GuildServers.Select(g => g.GuildId).Distinct().ToListAsync();

        var content = $"⚔️ A new Infinite Incursions event is scheduled to start <t:{incursion.EventStart.ToUnixTimeSeconds()}:R>!";

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
                guildId, GuildAlertChannelKind.InfiniteIncursions, GuildFeature.InfiniteIncursions, content, embed: embed);
        }

        incursion.NotifiedEventStart = incursion.EventStart;
        await db.SaveChangesAsync();
    }
}
