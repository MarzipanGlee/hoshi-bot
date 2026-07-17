using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceModalModule(AbsenceService absenceService) : ComponentInteractionModule<ModalInteractionContext>
{
    // Modals opened from a component (both of these are — see AbsenceButtonModule.CreateModal
    // and AbsenceStringMenuModule.EditTarget) can still ModifyMessage on submit: Discord
    // resolves it against the message the modal's originating component belonged to, so the
    // wizard keeps editing that same ephemeral message instead of posting a new one.
    [ComponentInteraction("absence-create-modal")]
    public Task CreateAbsence(string visibility, bool suppressNotifications) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var values = TextInputValues();
            if (!TryParseRange(values, out var startsAt, out var endsAt, out var error))
                return ErrorEdit(error!);

            var reason = values.GetValueOrDefault("reason");
            var draft = await absenceService.CreateDraftAsync(Context.Guild!.Id, Context.User.Id, startsAt, endsAt,
                string.IsNullOrWhiteSpace(reason) ? null : reason, Enum.Parse<AbsenceVisibility>(visibility), suppressNotifications);

            return DraftEdit(draft);
        });

    [ComponentInteraction("absence-edit-modal")]
    public Task EditAbsence(int absenceId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var values = TextInputValues();
            if (!TryParseRange(values, out var startsAt, out var endsAt, out var error))
                return ErrorEdit(error!);

            var reason = values.GetValueOrDefault("reason");
            var draft = await absenceService.CreateEditDraftAsync(absenceId, Context.User.Id, startsAt, endsAt,
                string.IsNullOrWhiteSpace(reason) ? null : reason);

            return draft is null
                ? ErrorEdit("Abwesenheit nicht gefunden oder keine Berechtigung.")
                : DraftEdit(draft);
        });

    private static Action<MessageOptions> DraftEdit(Absence draft) => m =>
    {
        m.Content = AbsenceService.BuildDraftSummary(draft);
        m.Embeds = [];
        m.Components = [new ActionRowProperties([AbsenceService.ConfirmButton(draft.Id), AbsenceService.CancelButton(draft.Id)])];
    };

    private static Action<MessageOptions> ErrorEdit(string message) => m => { m.Content = message; m.Embeds = []; m.Components = []; };

    // Dates/times are interpreted as UTC directly (Zurich-local precision is explicitly
    // deferred elsewhere in this project too, e.g. Territory Capture) — format TBD
    // against how the user actually types dates, flagged as an open item in the plan.
    private static bool TryParseRange(Dictionary<string, string> values, out DateTimeOffset startsAt, out DateTimeOffset endsAt, out string? error)
    {
        startsAt = default;
        endsAt = default;
        error = null;

        if (!TryParseDateTime(values.GetValueOrDefault("start-date"), values.GetValueOrDefault("start-time"), out startsAt))
        {
            error = "Startdatum/-zeit konnte nicht gelesen werden. Format: TT.MM.JJJJ / HH:MM.";
            return false;
        }

        if (!TryParseDateTime(values.GetValueOrDefault("end-date"), values.GetValueOrDefault("end-time"), out endsAt))
        {
            error = "Enddatum/-zeit konnte nicht gelesen werden. Format: TT.MM.JJJJ / HH:MM.";
            return false;
        }

        if (endsAt <= startsAt)
        {
            error = "Das Ende muss nach dem Start liegen.";
            return false;
        }

        return true;
    }

    private static bool TryParseDateTime(string? dateText, string? timeText, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(dateText) || string.IsNullOrWhiteSpace(timeText))
            return false;
        if (!DateOnly.TryParseExact(dateText.Trim(), "dd.MM.yyyy", out var date))
            return false;
        if (!TimeOnly.TryParseExact(timeText.Trim(), "HH:mm", out var time))
            return false;

        result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);
        return true;
    }

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
