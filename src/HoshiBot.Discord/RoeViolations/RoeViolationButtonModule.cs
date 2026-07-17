using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.RoeViolations;

public class RoeViolationButtonModule(RoeViolationService roeViolationService) : ComponentInteractionModule<ButtonInteractionContext>
{
    // Both actions do a Discord REST call (post the diplomat ping / archive+lock the thread) plus
    // DB work, which can exceed Discord's ~3s interaction deadline and show "interaction failed".
    // So ack with an ephemeral placeholder first, then edit it with the outcome — never a single
    // response built only at the end. The ephemeral reply is personal to the clicking user and
    // independent of the shared forum post the button lives on.
    [ComponentInteraction("roe-violation-ready")]
    public async Task SetReady(int reportId)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(EphemeralReply.Of("⏳ Einen Moment...")));
        var result = await roeViolationService.SetReadyForDiplomatAsync(reportId, Context.User.Id);
        await Context.Interaction.ModifyResponseAsync(m => m.Content = result);
    }

    [ComponentInteraction("roe-violation-done")]
    public async Task Close(int reportId)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(EphemeralReply.Of("⏳ Einen Moment...")));
        var result = await roeViolationService.CloseReportAsync(reportId, Context.User.Id);
        await Context.Interaction.ModifyResponseAsync(m => m.Content = result);
    }
}
