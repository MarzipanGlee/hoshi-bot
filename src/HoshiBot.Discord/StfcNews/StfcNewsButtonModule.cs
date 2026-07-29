using HoshiBot.Data;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.StfcNews;

public class StfcNewsButtonModule(StfcNewsService service, EmbedBranding embedBranding, LanguageResolver languageResolver) : ComponentInteractionModule<ButtonInteractionContext>
{
    // The modal and the ephemeral confirm results are personal to the clicking admin — their
    // language. The shared post itself is re-rendered per guild inside the service/jobs.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    // Both Enter Date and Edit open the same modal, routed to the same submit handler
    // (StfcNewsModalModule) — a resubmission via Edit is just a later SubmitDateAsync call,
    // there's no separate "edit" concept on the data side. Async so the modal renders in the
    // acting user's language — modals can't be deferred, but the cached resolver lookup is
    // well within Discord's 3s window.
    [ComponentInteraction("stfc-news-enter-date")]
    public async Task<InteractionCallbackProperties<ModalProperties>> EnterDate(int postId) => BuildDateModal(postId, await ActingUserLanguageAsync());

    [ComponentInteraction("stfc-news-edit")]
    public async Task<InteractionCallbackProperties<ModalProperties>> Edit(int postId) => BuildDateModal(postId, await ActingUserLanguageAsync());

    private static InteractionCallbackProperties<ModalProperties> BuildDateModal(int postId, Language lang) =>
        InteractionCallback.Modal(new ModalProperties($"stfc-news-date-modal:{postId}", Msg.News.EnterDateTitle(lang),
        [
            new LabelProperties(Msg.News.DateInputLabel(lang), new TextInputProperties("event-date", TextInputStyle.Short) { Placeholder = Msg.News.DatePlaceholder(lang), Required = true }),
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
            var lang = await ActingUserLanguageAsync();
            var (outcome, count, required) = await service.ConfirmDateAsync(postId, Context.User.Id);
            return outcome switch
            {
                StfcNewsActionOutcome.NotFound => Msg.News.PostNotFound(lang),
                StfcNewsActionOutcome.AlreadyResolved => Msg.News.AlreadyConfirmed(lang),
                StfcNewsActionOutcome.CannotConfirmOwnSubmission => Msg.News.CannotConfirmOwn(lang),
                StfcNewsActionOutcome.Resolved => Msg.News.FinalConfirmation(lang),
                _ => Msg.News.ConfirmationRecorded(lang, count, required),
            };
        });
}
