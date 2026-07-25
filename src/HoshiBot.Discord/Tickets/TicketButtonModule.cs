using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketButtonModule(TicketService ticketService, EmbedBranding embedBranding) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("ticket-close")]
    public Task CloseTicket(int ticketId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, () => ticketService.CloseTicketAsync(ticketId, Context.User.Id));
}
