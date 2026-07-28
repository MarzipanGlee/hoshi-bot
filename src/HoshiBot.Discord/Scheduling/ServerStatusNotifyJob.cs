using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Discord.Scheduling;

// Announces a server going up/down or in/out of maintenance to every guild tracking
// that server (via GuildServer). Reads StfcServerStatus rows whose observed Status or
// Maintenance no longer matches what was last announced — see StfcServerStatus for why
// that diff lives on the row instead of here. Currently only fed by a one-time seed
// (StfcServerStatusSeedData), not a live sync — stfc.pro's robots.txt disallows
// automated /api/ access, so nothing populates new observed values yet. This job is
// still correct once that data starts flowing from api.stfc.pro.
public class ServerStatusNotifyJob(
    HoshiBotDbContext db, NotificationDispatcher dispatcher, EmbedBranding embedBranding)
    : DiffNotifyJobBase<StfcServerStatus>(db, dispatcher, embedBranding)
{
    // All strings come from the message catalog (Msg.Server); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    protected override GuildAlertChannelKind ChannelKind => GuildAlertChannelKind.ServerStatus;
    protected override GuildFeature Feature => GuildFeature.ServerStatus;
    protected override string? Title => Msg.Server.StatusChangeTitle(Lang);

    protected override Task<List<StfcServerStatus>> LoadPendingRowsAsync() =>
        Db.StfcServerStatuses
            .Include(s => s.StfcServer)
            .Where(s => s.Status != s.NotifiedStatus || s.Maintenance != s.NotifiedMaintenance)
            .ToListAsync();

    protected override Task<List<ulong>> ResolveGuildIdsAsync(StfcServerStatus status) =>
        Db.GuildServers
            .Where(g => g.StfcServerId == status.StfcServerId)
            .Select(g => g.GuildId)
            .ToListAsync();

    protected override (string Content, NetCord.Color Color) BuildAnnouncement(StfcServerStatus status)
    {
        var serverName = status.StfcServer.DisplayName;

        if (status.Maintenance != "0")
            return (Msg.Server.Maintenance(Lang, serverName), EmbedBranding.WarningColor);

        if (status.Status != 1)
            return (Msg.Server.Down(Lang, serverName), EmbedBranding.DangerColor);

        return (Msg.Server.BackOnline(Lang, serverName), EmbedBranding.InformationColor);
    }

    protected override void MarkNotified(StfcServerStatus status)
    {
        status.NotifiedStatus = status.Status;
        status.NotifiedMaintenance = status.Maintenance;
    }
}
