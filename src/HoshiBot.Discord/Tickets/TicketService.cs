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
    GuildFeatureSettingsService settingsService,
    LanguageResolver languageResolver)
{
    public static ButtonProperties CloseButton(int ticketId, Language lang) =>
        new($"ticket-close:{ticketId}", Msg.Ticket.CloseButton(lang), EmojiProperties.Standard("✖️"), ButtonStyle.Danger);

    public static UserMenuProperties AddCommanderMenu(int ticketId, Language lang) =>
        new($"ticket-add-commander:{ticketId}") { Placeholder = Msg.Ticket.AddCommanderMenu(lang) };

    public async Task<string> OpenTicketAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, ulong openedByUserId, string openerName, string subject)
    {
        // The opener is both the acting user (ephemeral result) and the person the private
        // thread is dedicated to, so one resolved language covers the welcome message, its
        // components and the status strings; admin notifications use the guild language.
        var lang = await languageResolver.ForUserAsync(openedByUserId, scopeGuildId: guildId);

        var channelIdResult = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.Tickets, audience, guildAllianceId, TicketsSettingKeys.Channel);
        if (channelIdResult is not { } channelId)
            return Msg.Ticket.ChannelNotConfigured(lang);

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
            var guildLang = await languageResolver.ForGuildAsync(guildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.CreateTicketThread, channelId,
                BotPermission.ViewChannel | BotPermission.CreatePrivateThreads);
            return Msg.Ticket.Misconfigured(lang);
        }

        ticket.ThreadId = thread.Id;
        await db.SaveChangesAsync();

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(thread.Id, openedByUserId);

            var embed = await embedBranding.BuildBrandedAsync(guildId, Msg.Ticket.Welcome(lang, openerName));

            await gatewayClient.Rest.SendMessageAsync(thread.Id, new MessageProperties
            {
                Embeds = [embed],
                Components = [AddCommanderMenu(ticket.Id, lang), new ActionRowProperties([CloseButton(ticket.Id, lang)])],
            });
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // The thread exists even if this part fails — staff with channel permissions
            // can still see/use it, so there's nothing to roll back, just report it.
            var guildLang = await languageResolver.ForGuildAsync(guildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(guildId, BotAction.SendTicketWelcome, thread.Id,
                BotPermission.SendMessagesInThreads | BotPermission.EmbedLinks);
        }

        return Msg.Ticket.Created(lang, $"<#{thread.Id}>");
    }

    // callerLanguage: the acting user's resolved language — the status strings go back
    // ephemerally to whoever picked the commander, not to the added user. A Language
    // parameter (rather than a callerId) because the user-menu handler calls this in a
    // loop over the selection and resolves once.
    public async Task<string> AddCommanderAsync(int ticketId, ulong userId, Language callerLanguage)
    {
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null)
            return Msg.Ticket.NotFound(callerLanguage);

        try
        {
            await gatewayClient.Rest.AddGuildThreadUserAsync(ticket.ThreadId, userId);
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            var guildLang = await languageResolver.ForGuildAsync(ticket.GuildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(ticket.GuildId, BotAction.AddTicketCommander, ticket.ThreadId, BotPermission.SendMessagesInThreads);
            return Msg.Ticket.AddFailed(callerLanguage);
        }

        return Msg.Ticket.CommanderAdded(callerLanguage, $"<@{userId}>");
    }

    public async Task<string> CloseTicketAsync(int ticketId, ulong closedByUserId)
    {
        var ticket = await db.Tickets.FindAsync(ticketId);
        if (ticket is null)
            return Msg.Ticket.NotFound(await languageResolver.ForUserAsync(closedByUserId));

        // Everything rendered here is the ephemeral status back to the closing user —
        // archiving the thread posts nothing into it.
        var callerLang = await languageResolver.ForUserAsync(closedByUserId, scopeGuildId: ticket.GuildId);
        if (ticket.Status == TicketStatus.Closed)
            return Msg.Ticket.AlreadyClosed(callerLang);

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
            var guildLang = await languageResolver.ForGuildAsync(ticket.GuildId);
            await dispatcher.NotifyAdminOfPermissionIssueAsync(ticket.GuildId, BotAction.CloseTicket, ticket.ThreadId, BotPermission.ManageThreads);
            return Msg.Ticket.CloseFailed(callerLang);
        }

        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTimeOffset.UtcNow;
        ticket.ClosedByDiscordUserId = closedByUserId;
        await db.SaveChangesAsync();

        return Msg.Ticket.Closed(callerLang);
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
