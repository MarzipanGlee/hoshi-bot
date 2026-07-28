using System.Net;
using System.Text;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Tickets;

// Real thread creation/permissioning from scratch — unlike everything ported so far,
// legacy's Tickets is YAGPDB's own first-party plugin (createTicket/exec "Ticket Close"),
// not a custom Command; there's no built-in equivalent to lean on here.
public class TicketService(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    NotificationDispatcher dispatcher,
    EmbedBranding embedBranding,
    GuildFeatureSettingsService settingsService)
{
    // All strings come from the message catalog (Msg.Ticket); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    public static ButtonProperties CloseButton(int ticketId) =>
        new($"ticket-close:{ticketId}", Msg.Ticket.CloseButton(Lang), EmojiProperties.Standard("✖️"), ButtonStyle.Danger);

    public static UserMenuProperties AddCommanderMenu(int ticketId) =>
        new($"ticket-add-commander:{ticketId}") { Placeholder = Msg.Ticket.AddCommanderMenu(Lang) };

    public async Task<string> OpenTicketAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, ulong openedByUserId, string openerName, string subject)
    {
        var channelIdResult = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Tickets, audience, guildAllianceId, TicketsSettingKeys.Channel);
        if (channelIdResult is not { } channelId)
            return Msg.Ticket.ChannelNotConfigured(Lang);

        var ticket = new Ticket
        {
            GuildId = guildId,
            Subject = subject,
            OpenedByDiscordUserId = openedByUserId,
            Audience = audience,
            Status = TicketStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var threadName = $"{ticket.Id}-{Slugify(openerName)}-{Slugify(subject)}";
        if (threadName.Length > 100)
            threadName = threadName[..100];

        GuildThread thread;
        try
        {
            thread = await gatewayClient.Rest.CreateGuildThreadAsync(channelId,
                new GuildThreadProperties(threadName) { ChannelType = ChannelType.PrivateGuildThread });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            db.Tickets.Remove(ticket);
            await db.SaveChangesAsync();
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, Msg.Ticket.ActionCreate(Lang), Msg.Ticket.HintCreateThreads(Lang, $"<#{channelId}>"));
            return Msg.Ticket.Misconfigured(Lang);
        }

        ticket.ThreadId = thread.Id;
        await db.SaveChangesAsync();

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, openedByUserId);

            var embed = await embedBranding.BuildBrandedAsync(guildId, Msg.Ticket.Welcome(Lang, openerName));

            await gatewayClient.Rest.SendMessageAsync(thread.Id, new MessageProperties
            {
                Embeds = [embed],
                Components = [AddCommanderMenu(ticket.Id), new ActionRowProperties([CloseButton(ticket.Id)])],
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // The thread exists even if this part fails — staff with channel permissions
            // can still see/use it, so there's nothing to roll back, just report it.
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, Msg.Ticket.ActionSendWelcome(Lang), Msg.Ticket.HintThreadPermission(Lang, $"<#{thread.Id}>"));
        }

        return Msg.Ticket.Created(Lang, $"<#{thread.Id}>");
    }

    public async Task<string> AddCommanderAsync(int ticketId, ulong userId)
    {
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null)
            return Msg.Ticket.NotFound(Lang);

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(ticket.ThreadId, userId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(ticket.GuildId, Msg.Ticket.ActionAddCommander(Lang), Msg.Ticket.HintThreadPermission(Lang, $"<#{ticket.ThreadId}>"));
            return Msg.Ticket.AddFailed(Lang);
        }

        return Msg.Ticket.CommanderAdded(Lang, $"<@{userId}>");
    }

    public async Task<string> CloseTicketAsync(int ticketId, ulong closedByUserId)
    {
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null)
            return Msg.Ticket.NotFound(Lang);
        if (ticket.Status == TicketStatus.Closed)
            return Msg.Ticket.AlreadyClosed(Lang);

        try
        {
            await gatewayClient.Rest.ModifyGuildChannelAsync(ticket.ThreadId, o =>
            {
                o.Archived = true;
                o.Locked = true;
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(ticket.GuildId, Msg.Ticket.ActionClose(Lang), Msg.Ticket.HintManageThreads(Lang, $"<#{ticket.ThreadId}>"));
            return Msg.Ticket.CloseFailed(Lang);
        }

        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTimeOffset.UtcNow;
        ticket.ClosedByDiscordUserId = closedByUserId;
        await db.SaveChangesAsync();

        return Msg.Ticket.Closed(Lang);
    }

    private static string Slugify(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }

        return sb.ToString().Trim('-');
    }
}
