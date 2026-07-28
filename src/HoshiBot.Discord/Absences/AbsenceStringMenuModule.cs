using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceStringMenuModule(AbsenceService absenceService, EmbedBranding embedBranding) : ComponentInteractionModule<StringMenuInteractionContext>
{
    // All strings come from the message catalog (Msg.Absence); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    // Message and Modal callbacks share only this non-generic base — used here so one
    // handler can return either depending on whether the absence still exists.
    [ComponentInteraction("absence-edit-target")]
    public async Task<InteractionCallbackProperties> EditTarget()
    {
        var absenceId = int.Parse(Context.SelectedValues[0]);
        var absence = await absenceService.GetOwnAsync(absenceId, Context.User.Id);
        if (absence is null)
        {
            var errorEmbed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, Msg.Absence.NotFound(Lang));
            return InteractionCallback.ModifyMessage(m => { m.Embeds = [errorEmbed]; m.Components = []; });
        }

        // The dd.MM.yyyy/HH:mm prefill formats stay hardcoded: they must round-trip through
        // AbsenceModalModule.TryParseDateTime's parse formats, which 6e localizes together.
        return InteractionCallback.Modal(new ModalProperties($"absence-edit-modal:{absenceId}", Msg.Absence.EditTitle(Lang),
        [
            new LabelProperties(Msg.Absence.StartDateLabel(Lang), new TextInputProperties("start-date", TextInputStyle.Short)
                { Value = absence.StartsAt.ToString("dd.MM.yyyy"), Required = true }),
            new LabelProperties(Msg.Absence.StartTimeLabel(Lang), new TextInputProperties("start-time", TextInputStyle.Short)
                { Value = absence.StartsAt.ToString("HH:mm"), Required = true }),
            new LabelProperties(Msg.Absence.EndDateLabel(Lang), new TextInputProperties("end-date", TextInputStyle.Short)
                { Value = absence.EndsAt.ToString("dd.MM.yyyy"), Required = true }),
            new LabelProperties(Msg.Absence.EndTimeLabel(Lang), new TextInputProperties("end-time", TextInputStyle.Short)
                { Value = absence.EndsAt.ToString("HH:mm"), Required = true }),
            new LabelProperties(Msg.Absence.ReasonLabel(Lang), new TextInputProperties("reason", TextInputStyle.Short)
                { Value = absence.Reason, Placeholder = Msg.Absence.OptionalPlaceholder(Lang), Required = false }),
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
