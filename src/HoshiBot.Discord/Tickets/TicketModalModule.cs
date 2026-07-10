using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketModalModule(TicketService ticketService) : ComponentInteractionModule<ModalInteractionContext>
{
    // Always opened from CommandBridgeButtonModule.ContactCommandStaffPrompt's ephemeral
    // wizard message, so ModifyMessage is safe here — never the public hub.
    [ComponentInteraction("ticket-open-modal")]
    public async Task<InteractionCallbackProperties<MessageOptions>> OpenTicket(string audience)
    {
        var subject = Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value)
            .GetValueOrDefault("subject") ?? "";

        var result = await ticketService.OpenTicketAsync(
            Context.Guild!.Id, Enum.Parse<GuildAudience>(audience), Context.User.Id, CommanderName.Of(Context.User), subject);
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }
}
