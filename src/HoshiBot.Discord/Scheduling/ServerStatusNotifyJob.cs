using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Announces a server going up/down or in/out of maintenance to every guild tracking
// that server (via GuildServer). Reads StfcServerStatus rows whose observed Status or
// Maintenance no longer matches what was last announced — see StfcServerStatus for why
// that diff lives on the row instead of here. Currently only fed by a one-time seed
// (StfcServerStatusSeedData), not a live sync — stfc.pro's robots.txt disallows
// automated /api/ access, so nothing populates new observed values yet. This job is
// still correct once that data starts flowing from api.stfc.pro.
public class ServerStatusNotifyJob(HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var changed = await db.StfcServerStatuses
            .Include(s => s.StfcServer)
            .Where(s => s.Status != s.NotifiedStatus || s.Maintenance != s.NotifiedMaintenance)
            .ToListAsync();

        foreach (var status in changed)
        {
            var guildIds = await db.GuildServers
                .Where(g => g.StfcServerId == status.StfcServerId)
                .Select(g => g.GuildId)
                .ToListAsync();

            if (guildIds.Count == 0)
            {
                status.NotifiedStatus = status.Status;
                status.NotifiedMaintenance = status.Maintenance;
                continue;
            }

            var (content, color) = BuildAnnouncement(status);

            foreach (var guildId in guildIds)
            {
                var embed = new EmbedProperties
                {
                    Description = content,
                    Color = color,
                    Author = await embedBranding.BuildAuthorAsync(guildId),
                    Footer = embedBranding.BuildFooter(guildId),
                };
                await dispatcher.SendPublicAsync(guildId, GuildAlertChannelKind.ServerStatus, content, embed: embed);
            }

            status.NotifiedStatus = status.Status;
            status.NotifiedMaintenance = status.Maintenance;
        }

        await db.SaveChangesAsync();
    }

    private static (string Content, NetCord.Color Color) BuildAnnouncement(StfcServerStatus status)
    {
        var serverName = status.StfcServer.DisplayName;

        if (status.Maintenance != "0")
            return ($"🛠️ **{serverName}** is entering maintenance.", EmbedBranding.WarningColor);

        if (status.Status != 1)
            return ($"🔴 **{serverName}** is down.", EmbedBranding.DangerColor);

        return ($"🟢 **{serverName}** is back online.", EmbedBranding.InformationColor);
    }
}
