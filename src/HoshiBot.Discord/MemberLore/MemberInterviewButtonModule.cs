using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.MemberLore;

// The "Nein danke" opt-out button on the interview opener DM. Acks by removing the button from the
// (bot's own, private) DM message, then records the decline and sends the closing message.
public class MemberInterviewButtonModule(MemberInterviewService interviews) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction(MemberInterviewService.DeclineButtonId)]
    public async Task Decline()
    {
        // Ack first (fast, no DB) by dropping the button so it can't be clicked twice.
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(m => m.Components = []));
        await interviews.DeclineAsync(Context.User.Id, CancellationToken.None);
    }
}
