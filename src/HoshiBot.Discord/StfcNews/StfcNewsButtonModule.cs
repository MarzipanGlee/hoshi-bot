using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.StfcNews;

public class StfcNewsButtonModule(StfcNewsService service, EmbedBranding embedBranding) : ComponentInteractionModule<ButtonInteractionContext>
{
    // All strings come from the message catalog (Msg.News); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    // Both Enter Date and Edit open the same modal, routed to the same submit handler
    // (StfcNewsModalModule) — a resubmission via Edit is just a later SubmitDateAsync call,
    // there's no separate "edit" concept on the data side.
    [ComponentInteraction("stfc-news-enter-date")]
    public InteractionCallbackProperties<ModalProperties> EnterDate(int postId) => BuildDateModal(postId);

    [ComponentInteraction("stfc-news-edit")]
    public InteractionCallbackProperties<ModalProperties> Edit(int postId) => BuildDateModal(postId);

    private static InteractionCallbackProperties<ModalProperties> BuildDateModal(int postId) =>
        InteractionCallback.Modal(new ModalProperties($"stfc-news-date-modal:{postId}", Msg.News.EnterDateTitle(Lang),
        [
            new LabelProperties(Msg.News.DateInputLabel(Lang), new TextInputProperties("event-date", TextInputStyle.Short) { Placeholder = Msg.News.DatePlaceholder(Lang), Required = true }),
        ]));

    // Personal ephemeral reply, not an edit to the shared message: an immediate "Processing"
    // ack (ConfirmDateAsync's DB work — recount, trusted-user check, and on the resolving
    // click a StfcEventStatus write — can occasionally exceed Discord's 3-second interaction
    // deadline), then edited with the real outcome once it completes. The shared message (seen by
    // everyone) is updated separately, inside the service, via a plain direct REST edit — kept
    // fully independent of this reply so the two can never conflict.
    [ComponentInteraction("stfc-news-confirm")]
    public Task Confirm(int postId) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var (outcome, count, required) = await service.ConfirmDateAsync(postId, Context.User.Id);
            return outcome switch
            {
                StfcNewsActionOutcome.NotFound => Msg.News.PostNotFound(Lang),
                StfcNewsActionOutcome.AlreadyResolved => Msg.News.AlreadyConfirmed(Lang),
                StfcNewsActionOutcome.CannotConfirmOwnSubmission => Msg.News.CannotConfirmOwn(Lang),
                StfcNewsActionOutcome.Resolved => Msg.News.FinalConfirmation(Lang),
                _ => Msg.News.ConfirmationRecorded(Lang, count, required),
            };
        });
}
