using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Tickets;

public class TicketModalModule(TicketService ticketService, GuildAllianceService allianceService, EmbedBranding embedBranding) : ComponentInteractionModule<ModalInteractionContext>
{
    // Always opened from CommandBridgeButtonModule.ContactCommandStaffPrompt's ephemeral
    // wizard message, so ModifyMessage is safe here — never the public hub.
    [ComponentInteraction("ticket-open-modal")]
    public Task OpenTicket(string audience) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var subject = Context.Components
                .OfType<Label>()
                .Select(l => l.Component)
                .OfType<TextInput>()
                .ToDictionary(i => i.CustomId, i => i.Value)
                .GetValueOrDefault("subject") ?? "";

            var (parsedAudience, guildAllianceId, _) = await allianceService.ResolveScopeAsync(Context.Guild!.Id, audience);
            var result = await ticketService.OpenTicketAsync(
                Context.Guild!.Id, parsedAudience, guildAllianceId, Context.User.Id, CommanderName.Of(Context.User), subject);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });
}
