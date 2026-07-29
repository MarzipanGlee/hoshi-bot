using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.Absences;

public class AbsenceModalModule(AbsenceService absenceService, LanguageResolver languageResolver) : ComponentInteractionModule<ModalInteractionContext>
{
    // The submit result edits the acting user's own ephemeral wizard message.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    // Modals opened from a component (both of these are — see AbsenceButtonModule.CreateModal
    // and AbsenceStringMenuModule.EditTarget) can still ModifyMessage on submit: Discord
    // resolves it against the message the modal's originating component belonged to, so the
    // wizard keeps editing that same ephemeral message instead of posting a new one.
    [ComponentInteraction("absence-create-modal")]
    public Task CreateAbsence(string visibility, bool suppressNotifications) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var values = TextInputValues();
            if (!TryParseRange(values, lang, out var startsAt, out var endsAt, out var error))
                return ErrorEdit(error!);

            var reason = values.GetValueOrDefault("reason");
            var draft = await absenceService.CreateDraftAsync(Context.Guild!.Id, Context.User.Id, startsAt, endsAt,
                string.IsNullOrWhiteSpace(reason) ? null : reason, Enum.Parse<AbsenceVisibility>(visibility), suppressNotifications);

            return DraftEdit(draft, lang);
        });

    [ComponentInteraction("absence-edit-modal")]
    public Task EditAbsence(int absenceId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();
            var values = TextInputValues();
            if (!TryParseRange(values, lang, out var startsAt, out var endsAt, out var error))
                return ErrorEdit(error!);

            var reason = values.GetValueOrDefault("reason");
            var draft = await absenceService.CreateEditDraftAsync(absenceId, Context.User.Id, startsAt, endsAt,
                string.IsNullOrWhiteSpace(reason) ? null : reason);

            return draft is null
                ? ErrorEdit(Msg.Absence.NotFoundOrNoPermission(lang))
                : DraftEdit(draft, lang);
        });

    private static Action<MessageOptions> DraftEdit(Absence draft, Language lang) => m =>
    {
        m.Content = AbsenceService.BuildDraftSummary(draft, lang);
        m.Embeds = [];
        m.Components = [new ActionRowProperties([AbsenceService.ConfirmButton(draft.Id, lang), AbsenceService.CancelButton(draft.Id, lang)])];
    };

    private static Action<MessageOptions> ErrorEdit(string message) => m => { m.Content = message; m.Embeds = []; m.Components = []; };

    // Dates/times are interpreted as UTC directly (Zurich-local precision is explicitly
    // deferred elsewhere in this project too, e.g. Territory Capture); the accepted text
    // formats are per-language via DateInput — permissive: the resolved language's
    // convention plus the German dd.MM.yyyy and ISO fallbacks.
    private static bool TryParseRange(Dictionary<string, string> values, Language lang, out DateTimeOffset startsAt, out DateTimeOffset endsAt, out string? error)
    {
        startsAt = default;
        endsAt = default;
        error = null;

        if (!TryParseDateTime(values.GetValueOrDefault("start-date"), values.GetValueOrDefault("start-time"), lang, out startsAt))
        {
            error = Msg.Absence.StartParseError(lang);
            return false;
        }

        if (!TryParseDateTime(values.GetValueOrDefault("end-date"), values.GetValueOrDefault("end-time"), lang, out endsAt))
        {
            error = Msg.Absence.EndParseError(lang);
            return false;
        }

        if (endsAt <= startsAt)
        {
            error = Msg.Absence.EndMustBeAfterStart(lang);
            return false;
        }

        return true;
    }

    private static bool TryParseDateTime(string? dateText, string? timeText, Language lang, out DateTimeOffset result)
    {
        result = default;
        if (!DateInput.TryParseDate(dateText, lang, out var date))
            return false;
        if (!DateInput.TryParseTime(timeText, lang, out var time))
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
