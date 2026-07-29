using HoshiBot.Data;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceStringMenuModule(AbsenceService absenceService, EmbedBranding embedBranding, LanguageResolver languageResolver) : ComponentInteractionModule<StringMenuInteractionContext>
{
    // Message and Modal callbacks share only this non-generic base — used here so one
    // handler can return either depending on whether the absence still exists.
    [ComponentInteraction("absence-edit-target")]
    public async Task<InteractionCallbackProperties> EditTarget()
    {
        // The error edit and the modal are both for the acting user alone.
        var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

        var absenceId = int.Parse(Context.SelectedValues[0]);
        var absence = await absenceService.GetOwnAsync(absenceId, Context.User.Id);
        if (absence is null)
        {
            var errorEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Absence.NotFound(lang));
            return InteractionCallback.ModifyMessage(m => { m.Embeds = [errorEmbed]; m.Components = []; });
        }

        // Prefill via DateInput in this language's own convention (de dd.MM.yyyy, otherwise
        // ISO) so the prefilled values round-trip unchanged through the same DateInput
        // parsing AbsenceModalModule.TryParseDateTime applies on submit.
        return InteractionCallback.Modal(new ModalProperties($"absence-edit-modal:{absenceId}", Msg.Absence.EditTitle(lang),
        [
            new LabelProperties(Msg.Absence.StartDateLabel(lang), new TextInputProperties("start-date", TextInputStyle.Short)
                { Value = DateInput.FormatDate(absence.StartsAt, lang), Required = true }),
            new LabelProperties(Msg.Absence.StartTimeLabel(lang), new TextInputProperties("start-time", TextInputStyle.Short)
                { Value = DateInput.FormatTime(absence.StartsAt), Required = true }),
            new LabelProperties(Msg.Absence.EndDateLabel(lang), new TextInputProperties("end-date", TextInputStyle.Short)
                { Value = DateInput.FormatDate(absence.EndsAt, lang), Required = true }),
            new LabelProperties(Msg.Absence.EndTimeLabel(lang), new TextInputProperties("end-time", TextInputStyle.Short)
                { Value = DateInput.FormatTime(absence.EndsAt), Required = true }),
            new LabelProperties(Msg.Absence.ReasonLabel(lang), new TextInputProperties("reason", TextInputStyle.Short)
                { Value = absence.Reason, Placeholder = Msg.Absence.OptionalPlaceholder(lang), Required = false }),
        ]));
    }

    [ComponentInteraction("absence-delete-target")]
    public Task DeleteTarget() =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var absenceId = int.Parse(Context.SelectedValues[0]);
            var result = await absenceService.DeleteAsync(absenceId, Context.User.Id);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });
}
