using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.AnonymousMessages;

public class AnonymousMessageModalModule(AnonymousMessageService anonymousMessageService) : ComponentInteractionModule<ModalInteractionContext>
{
    // Always opened from CommandBridgeButtonModule.ContactCommandStaffPrompt's ephemeral
    // wizard message, so ModifyMessage is safe here — never the public hub.
    [ComponentInteraction("anonymous-message-modal")]
    public async Task<InteractionCallbackProperties<MessageOptions>> SendAnonymousMessage(string audience)
    {
        var values = Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);

        var subject = values.GetValueOrDefault("subject") ?? "";
        var message = values.GetValueOrDefault("message") ?? "";

        var result = await anonymousMessageService.SendAsync(Context.Guild!.Id, Enum.Parse<GuildAudience>(audience), subject, message);
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }
}
