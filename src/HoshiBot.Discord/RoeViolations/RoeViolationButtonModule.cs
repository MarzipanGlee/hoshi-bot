using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.RoeViolations;

public class RoeViolationButtonModule(RoeViolationService roeViolationService) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("roe-violation-ready")]
    public async Task<InteractionMessageProperties> SetReady(int reportId) =>
        EphemeralReply.Of(await roeViolationService.SetReadyForDiplomatAsync(reportId, Context.User.Id));

    [ComponentInteraction("roe-violation-done")]
    public async Task<InteractionMessageProperties> Close(int reportId) =>
        EphemeralReply.Of(await roeViolationService.CloseReportAsync(reportId, Context.User.Id));
}
