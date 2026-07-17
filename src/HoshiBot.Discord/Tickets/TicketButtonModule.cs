using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketButtonModule(TicketService ticketService) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("ticket-close")]
    public Task CloseTicket(int ticketId) =>
        Context.Interaction.SendDelayedResponseAsync(() => ticketService.CloseTicketAsync(ticketId, Context.User.Id));
}
