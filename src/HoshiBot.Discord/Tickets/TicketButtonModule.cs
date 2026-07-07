using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketButtonModule(TicketService ticketService) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("ticket-close")]
    public async Task<InteractionMessageProperties> CloseTicket(int ticketId) =>
        EphemeralReply.Of(await ticketService.CloseTicketAsync(ticketId, Context.User.Id));
}
