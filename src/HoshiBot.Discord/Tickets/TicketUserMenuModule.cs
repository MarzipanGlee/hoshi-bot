using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketUserMenuModule(TicketService ticketService, EmbedBranding embedBranding) : ComponentInteractionModule<UserMenuInteractionContext>
{
    [ComponentInteraction("ticket-add-commander")]
    public Task AddCommander(int ticketId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var results = new List<string>();
            foreach (var user in Context.SelectedValues)
            {
                results.Add(await ticketService.AddCommanderAsync(ticketId, user.Id));
            }

            return string.Join('\n', results);
        });
}
