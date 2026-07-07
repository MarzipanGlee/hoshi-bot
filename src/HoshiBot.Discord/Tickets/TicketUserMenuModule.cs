using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketUserMenuModule(TicketService ticketService) : ComponentInteractionModule<UserMenuInteractionContext>
{
    [ComponentInteraction("ticket-add-commander")]
    public async Task<InteractionMessageProperties> AddCommander(int ticketId)
    {
        var results = new List<string>();
        foreach (var user in Context.SelectedValues)
        {
            results.Add(await ticketService.AddCommanderAsync(ticketId, user.Id));
        }

        return EphemeralReply.Of(string.Join('\n', results));
    }
}
