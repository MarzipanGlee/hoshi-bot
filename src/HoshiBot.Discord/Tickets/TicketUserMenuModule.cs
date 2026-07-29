using HoshiBot.Data;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketUserMenuModule(TicketService ticketService, EmbedBranding embedBranding, LanguageResolver languageResolver) : ComponentInteractionModule<UserMenuInteractionContext>
{
    [ComponentInteraction("ticket-add-commander")]
    public Task AddCommander(int ticketId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            // The per-user status lines are ephemeral to whoever picked the commanders —
            // resolved once here rather than per selected user.
            var callerLanguage = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);
            var results = new List<string>();
            foreach (var user in Context.SelectedValues)
            {
                results.Add(await ticketService.AddCommanderAsync(ticketId, user.Id, callerLanguage));
            }

            return string.Join('\n', results);
        });
}
