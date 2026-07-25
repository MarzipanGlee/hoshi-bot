using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.RoeViolations;

public class RoeViolationButtonModule(RoeViolationService roeViolationService, EmbedBranding embedBranding) : ComponentInteractionModule<ButtonInteractionContext>
{
    // Both do a Discord REST call (post the diplomat ping / archive+lock the thread) plus DB work,
    // so they ack immediately and edit afterwards via the shared helper (see
    // InteractionResponseExtensions) — never a single response built at the end.
    [ComponentInteraction("roe-violation-ready")]
    public Task SetReady(int reportId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, () => roeViolationService.SetReadyForDiplomatAsync(reportId, Context.User.Id));

    [ComponentInteraction("roe-violation-done")]
    public Task Close(int reportId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, () => roeViolationService.CloseReportAsync(reportId, Context.User.Id));
}
