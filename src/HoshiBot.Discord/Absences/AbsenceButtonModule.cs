using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceButtonModule(AbsenceService absenceService, EmbedBranding embedBranding, LanguageResolver languageResolver) : ComponentInteractionModule<ButtonInteractionContext>
{
    // Every step of this wizard is ephemeral to the acting user, so everything renders
    // in that user's language.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    // Every wizard step after the entry point edits the same ephemeral message via ModifyMessage
    // (the wizard experience the legacy bot had) instead of stacking a fresh ephemeral per step.
    private async Task<InteractionCallbackProperties<MessageOptions>> EphemeralEmbedModifyAsync(string description, IReadOnlyList<IMessageComponentProperties>? components = null, string? title = null)
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: title);
        return InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [embed];
            m.Components = components ?? [];
        });
    }

    // Fetching upcoming absences can take a moment under load, so — matching legacy's own
    // two-step "wird gesucht..." placeholder before the query, then editing the response
    // once it's done — this sends the loading state itself and edits it in place, rather
    // than returning a value for the framework to send only once everything is ready.
    [ComponentInteraction("absence-manage")]
    public Task ManageAbsences() =>
        Context.Interaction.SendDelayedEditAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
            var hasOwn = own.Count > 0;

            // Edit/Delete are always shown, just disabled when there's nothing to act on yet —
            // matches the legacy bot's behavior instead of hiding the buttons outright.
            var buttons = new List<ButtonProperties>
            {
                new("absence-create", Msg.Absence.CreateTitle(lang), EmojiProperties.Standard(Icons.Add), ButtonStyle.Success),
                new ButtonProperties("absence-edit", Msg.Absence.EditTitle(lang), EmojiProperties.Standard(Icons.Edit), ButtonStyle.Primary) { Disabled = !hasOwn },
                new ButtonProperties("absence-delete", Msg.Absence.DeleteTitle(lang), EmojiProperties.Standard(Icons.Cancel), ButtonStyle.Danger) { Disabled = !hasOwn },
            };

            var description = Msg.Absence.ManageIntro(lang, CommanderName.Of(Context.User), AbsenceService.BuildOwnListText(own, lang));

            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: Msg.Absence.ManageTitle(lang));

            return m =>
            {
                m.Embeds = [finalEmbed];
                m.Components = [new ActionRowProperties(buttons)];
            };
        });

    [ComponentInteraction("absence-create")]
    public async Task<InteractionCallbackProperties<MessageOptions>> CreatePrompt()
    {
        var lang = await ActingUserLanguageAsync();
        return await EphemeralEmbedModifyAsync(
            Msg.Absence.VisibilityPrompt(lang, CommanderName.Of(Context.User)),
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"absence-create-vis:{AbsenceVisibility.Public}", Msg.Absence.PublicButton(lang), EmojiProperties.Standard(Icons.Public), ButtonStyle.Primary),
                    new ButtonProperties($"absence-create-vis:{AbsenceVisibility.StaffOnly}", Msg.Absence.StaffButton(lang), EmojiProperties.Standard(Icons.StaffOnly), ButtonStyle.Primary),
                ]),
            ],
            title: Msg.Absence.CreateTitle(lang));
    }

    // Read as a plain string, not bound as an AbsenceVisibility parameter directly —
    // component-interaction enum-from-custom-id binding isn't verified, same caution
    // already applied to Raid's location/RoE's branch.
    [ComponentInteraction("absence-create-vis")]
    public async Task<InteractionCallbackProperties<MessageOptions>> NotificationsPrompt(string visibility)
    {
        var lang = await ActingUserLanguageAsync();
        return await EphemeralEmbedModifyAsync(
            Msg.Absence.NotificationsPrompt(lang),
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"absence-create-notify:{visibility}:true", Msg.Absence.NotificationsOffButton(lang), EmojiProperties.Standard(Icons.RemindersOff), ButtonStyle.Secondary),
                    new ButtonProperties($"absence-create-notify:{visibility}:false", Msg.Absence.NotificationsOnButton(lang), EmojiProperties.Standard(Icons.RemindersOn), ButtonStyle.Primary),
                ]),
            ]);
    }

    // Must stay a Modal response — Discord has no way to show a modal via ModifyMessage —
    // but since it was opened from a component, submitting it can still ModifyMessage the
    // originating message (see AbsenceModalModule.CreateAbsence). Async so the modal
    // renders in the acting user's language: modals can't be deferred, so the language
    // resolution has to happen before the callback is returned.
    [ComponentInteraction("absence-create-notify")]
    public async Task<InteractionCallbackProperties<ModalProperties>> CreateModal(string visibility, bool suppressNotifications)
    {
        var lang = await ActingUserLanguageAsync();
        return InteractionCallback.Modal(new ModalProperties($"absence-create-modal:{visibility}:{suppressNotifications}", Msg.Absence.CreateTitle(lang),
        [
            new LabelProperties(Msg.Absence.StartDateLabel(lang), new TextInputProperties("start-date", TextInputStyle.Short) { Placeholder = Msg.Absence.DatePlaceholder(lang), Required = true }),
            new LabelProperties(Msg.Absence.StartTimeLabel(lang), new TextInputProperties("start-time", TextInputStyle.Short) { Placeholder = Msg.Absence.TimePlaceholder(lang), Required = true }),
            new LabelProperties(Msg.Absence.EndDateLabel(lang), new TextInputProperties("end-date", TextInputStyle.Short) { Placeholder = Msg.Absence.DatePlaceholder(lang), Required = true }),
            new LabelProperties(Msg.Absence.EndTimeLabel(lang), new TextInputProperties("end-time", TextInputStyle.Short) { Placeholder = Msg.Absence.TimePlaceholder(lang), Required = true }),
            new LabelProperties(Msg.Absence.ReasonLabel(lang), new TextInputProperties("reason", TextInputStyle.Short) { Placeholder = Msg.Absence.OptionalPlaceholder(lang), Required = false }),
        ]));
    }

    // Same loading-then-edit pattern as ManageAbsences, just via ModifyMessage instead of a
    // brand-new message — this is itself a follow-up step within the wizard, not the entry
    // point, so both the loading state and the final response edit the same message.
    [ComponentInteraction("absence-edit")]
    public Task EditPrompt() =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Absence.EditPickPrompt(lang, CommanderName.Of(Context.User)),
                title: Msg.Absence.EditTitle(lang));

            return m =>
            {
                m.Embeds = [finalEmbed];
                m.Components = [new StringMenuProperties("absence-edit-target", own.Select(a => AbsenceService.BuildOption(a, lang)))];
            };
        });

    [ComponentInteraction("absence-delete")]
    public Task DeletePrompt() =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var own = await absenceService.GetOwnUpcomingAsync(Context.Guild!.Id, Context.User.Id);
            var finalEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Absence.DeletePickPrompt(lang, CommanderName.Of(Context.User)),
                title: Msg.Absence.DeleteTitle(lang));

            return m =>
            {
                m.Embeds = [finalEmbed];
                m.Components = [new StringMenuProperties("absence-delete-target", own.Select(a => AbsenceService.BuildOption(a, lang)))];
            };
        });

    [ComponentInteraction("absence-confirm")]
    public Task Confirm(int draftId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var result = await absenceService.ConfirmDraftAsync(draftId, Context.User.Id);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });

    [ComponentInteraction("absence-cancel")]
    public Task Cancel(int draftId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var result = await absenceService.CancelDraftAsync(draftId, Context.User.Id);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });
}
