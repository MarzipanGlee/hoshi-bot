using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceStringMenuModule(AbsenceService absenceService, EmbedBranding embedBranding) : ComponentInteractionModule<StringMenuInteractionContext>
{
    // Message and Modal callbacks share only this non-generic base — used here so one
    // handler can return either depending on whether the absence still exists.
    [ComponentInteraction("absence-edit-target")]
    public async Task<InteractionCallbackProperties> EditTarget()
    {
        var absenceId = int.Parse(Context.SelectedValues[0]);
        var absence = await absenceService.GetOwnAsync(absenceId, Context.User.Id);
        if (absence is null)
        {
            var guildId = Context.Guild!.Id;
            var errorEmbed = new EmbedProperties
            {
                Description = "Abwesenheit nicht gefunden.",
                Color = EmbedBranding.BotColor,
                Author = await embedBranding.BuildAuthorAsync(guildId),
                Footer = embedBranding.BuildFooter(guildId),
            };
            return InteractionCallback.ModifyMessage(m => { m.Embeds = [errorEmbed]; m.Components = []; });
        }

        return InteractionCallback.Modal(new ModalProperties($"absence-edit-modal:{absenceId}", "Abwesenheit bearbeiten",
        [
            new LabelProperties("Startdatum", new TextInputProperties("start-date", TextInputStyle.Short)
                { Value = absence.StartsAt.ToString("dd.MM.yyyy"), Required = true }),
            new LabelProperties("Startzeit", new TextInputProperties("start-time", TextInputStyle.Short)
                { Value = absence.StartsAt.ToString("HH:mm"), Required = true }),
            new LabelProperties("Enddatum", new TextInputProperties("end-date", TextInputStyle.Short)
                { Value = absence.EndsAt.ToString("dd.MM.yyyy"), Required = true }),
            new LabelProperties("Endzeit", new TextInputProperties("end-time", TextInputStyle.Short)
                { Value = absence.EndsAt.ToString("HH:mm"), Required = true }),
            new LabelProperties("Grund", new TextInputProperties("reason", TextInputStyle.Short)
                { Value = absence.Reason, Placeholder = "Optional", Required = false }),
        ]));
    }

    [ComponentInteraction("absence-delete-target")]
    public async Task<InteractionCallbackProperties<MessageOptions>> DeleteTarget()
    {
        var absenceId = int.Parse(Context.SelectedValues[0]);
        var result = await absenceService.DeleteAsync(absenceId, Context.User.Id);
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }
}
