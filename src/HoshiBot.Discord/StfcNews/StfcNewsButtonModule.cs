using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.StfcNews;

public class StfcNewsButtonModule(StfcNewsService service) : ComponentInteractionModule<ButtonInteractionContext>
{
    // Both Enter Date and Edit open the same modal, routed to the same submit handler
    // (StfcNewsModalModule) — a resubmission via Edit is just a later SubmitDateAsync call,
    // there's no separate "edit" concept on the data side.
    [ComponentInteraction("stfc-news-enter-date")]
    public InteractionCallbackProperties<ModalProperties> EnterDate(int postId) => BuildDateModal(postId);

    [ComponentInteraction("stfc-news-edit")]
    public InteractionCallbackProperties<ModalProperties> Edit(int postId) => BuildDateModal(postId);

    private static InteractionCallbackProperties<ModalProperties> BuildDateModal(int postId) =>
        InteractionCallback.Modal(new ModalProperties($"stfc-news-date-modal:{postId}", "Enter Event Date",
        [
            new LabelProperties("Event date", new TextInputProperties("event-date", TextInputStyle.Short) { Placeholder = "DD.MM.YYYY", Required = true }),
        ]));

    // Personal ephemeral reply, not an edit to the shared message: an immediate "Processing"
    // ack (ConfirmDateAsync's DB work — recount, trusted-user check, and on the resolving
    // click a StfcEventStatus write — can occasionally exceed Discord's 3-second interaction
    // deadline, e.g. under contention from the Web admin app sharing the same dev SQLite
    // file), then edited with the real outcome once it completes. The shared message (seen by
    // everyone) is updated separately, inside the service, via a plain direct REST edit — kept
    // fully independent of this reply so the two can never conflict.
    [ComponentInteraction("stfc-news-confirm")]
    public async Task Confirm(int postId)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(EphemeralReply.Of("⏳ Processing...")));

        var (outcome, count, required) = await service.ConfirmDateAsync(postId, Context.User.Id);
        await Context.Interaction.ModifyResponseAsync(m => m.Content = outcome switch
        {
            StfcNewsActionOutcome.NotFound => "This post could no longer be found.",
            StfcNewsActionOutcome.AlreadyResolved => "This event date has already been confirmed.",
            StfcNewsActionOutcome.CannotConfirmOwnSubmission => "You submitted this date — another admin needs to confirm it.",
            StfcNewsActionOutcome.Resolved => "Thanks — that was the final confirmation needed. The event date has been confirmed!",
            _ => $"Thanks — your confirmation has been recorded. ({count}/{required} confirmed).",
        });
    }
}
