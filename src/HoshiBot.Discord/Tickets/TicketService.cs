using System.Net;
using System.Text;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace HoshiBot.Discord.Tickets;

// Real thread creation/permissioning from scratch — unlike everything ported so far,
// legacy's Tickets is YAGPDB's own first-party plugin (createTicket/exec "Ticket Close"),
// not a custom Command; there's no built-in equivalent to lean on here.
public class TicketService(HoshiBotDbContext db, GatewayClient gatewayClient, NotificationDispatcher dispatcher, EmbedBranding embedBranding)
{
    private const string WelcomeMessageFormat =
        "Willkommen Commander {0}!\n\nBitte beschreibe den Grund für die Eröffnung dieses Tickets und füge alle relevanten Informationen bei, wie z. B. Beweise, weitere Commander usw.";

    public static ButtonProperties CloseButton(int ticketId) =>
        new($"ticket-close:{ticketId}", "Ticket schliessen", EmojiProperties.Standard("✖️"), ButtonStyle.Danger);

    public static UserMenuProperties AddCommanderMenu(int ticketId) =>
        new($"ticket-add-commander:{ticketId}") { Placeholder = "Commander zum Ticket hinzufügen" };

    public async Task<string> OpenTicketAsync(ulong guildId, ulong openedByUserId, string openerName, string subject)
    {
        var settings = await db.GuildSettings.FindAsync(guildId);
        if (settings?.TicketsChannelId is not { } channelId)
            return "Der Tickets-Kanal ist noch nicht konfiguriert (siehe Guild-Einstellungen).";

        var ticket = new Ticket
        {
            GuildId = guildId,
            Subject = subject,
            OpenedByDiscordUserId = openedByUserId,
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
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "ein Ticket erstellen", $"fehlende Berechtigung (Threads erstellen) in <#{channelId}>?");
            return "Das Ticket-System ist aktuell falsch konfiguriert — ein Admin wurde informiert.";
        }

        ticket.ThreadId = thread.Id;
        await db.SaveChangesAsync();

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, openedByUserId);

            var embed = new EmbedProperties
            {
                Description = string.Format(WelcomeMessageFormat, openerName),
                Color = EmbedBranding.BotColor,
                Author = await embedBranding.BuildAuthorAsync(guildId),
                Footer = embedBranding.BuildFooter(guildId),
            };

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
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, "die Ticket-Willkommensnachricht senden", $"fehlende Berechtigung im Thread <#{thread.Id}>?");
        }

        return $"Ticket erstellt: <#{thread.Id}>";
    }

    public async Task<string> AddCommanderAsync(int ticketId, ulong userId)
    {
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null)
            return "Ticket nicht gefunden.";

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(ticket.ThreadId, userId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            await dispatcher.NotifyAdminOfPermissionIssueAsync(ticket.GuildId, "einen Commander zum Ticket hinzufügen", $"fehlende Berechtigung im Thread <#{ticket.ThreadId}>?");
            return "Commander konnte nicht hinzugefügt werden — ein Admin wurde informiert.";
        }

        return $"<@{userId}> wurde zum Ticket hinzugefügt.";
    }

    public async Task<string> CloseTicketAsync(int ticketId, ulong closedByUserId)
    {
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null)
            return "Ticket nicht gefunden.";
        if (ticket.Status == TicketStatus.Closed)
            return "Dieses Ticket ist bereits geschlossen.";

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
            await dispatcher.NotifyAdminOfPermissionIssueAsync(ticket.GuildId, "das Ticket schliessen", $"fehlende Manage-Threads-Berechtigung im Thread <#{ticket.ThreadId}>?");
            return "Ticket konnte nicht geschlossen werden — ein Admin wurde informiert.";
        }

        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTimeOffset.UtcNow;
        ticket.ClosedByDiscordUserId = closedByUserId;
        await db.SaveChangesAsync();

        return "Ticket geschlossen.";
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
