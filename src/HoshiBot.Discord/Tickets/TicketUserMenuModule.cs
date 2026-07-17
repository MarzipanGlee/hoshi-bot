using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketUserMenuModule(TicketService ticketService) : ComponentInteractionModule<UserMenuInteractionContext>
{
    [ComponentInteraction("ticket-add-commander")]
    public Task AddCommander(int ticketId) =>
        Context.Interaction.SendDelayedResponseAsync(async () =>
        {
            var results = new List<string>();
            foreach (var user in Context.SelectedValues)
            {
                results.Add(await ticketService.AddCommanderAsync(ticketId, user.Id));
            }

            return string.Join('\n', results);
        });
}
