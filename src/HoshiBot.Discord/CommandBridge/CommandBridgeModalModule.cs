using HoshiBot.Data;
using HoshiBot.Discord.Alerts;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

public class CommandBridgeModalModule(AlertService alertService, PendingModalInputService pendingModalInputService, EmbedBranding embedBranding,
    LanguageResolver languageResolver)
    : ComponentInteractionModule<ModalInteractionContext>
{
    // The retry step is ephemeral to whoever submitted the modal — their language.
    private Task<Language> ActingUserLanguageAsync() =>
        languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

    // The "invalid input, try again" edit (retry/cancel buttons). Applied to whichever response
    // the caller acked: Raid edits its own ephemeral wizard message in place (ModifyDelayed),
    // while Shield Reminder — opened directly from the shared hub — acks a NEW ephemeral
    // (SendDelayedEdit) so this private prompt never lands on the public hub message. The embed
    // uses the same branded style as every real bot message, with a Zurück (reopen the modal,
    // pre-filled) / Abbrechen pair so a typo doesn't force restarting the whole flow.
    private async Task<Action<MessageOptions>> RetryEditAsync(string description, int pendingId, Language lang)
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: Msg.Bridge.InvalidInputTitle(lang));
        return m =>
        {
            // Clear the "⏳ Processing..." placeholder — otherwise it lingers as plain text above the
            // error embed (the ack was content-only; without this the edit only adds the embed).
            m.Content = "";
            m.Embeds = [embed];
            m.Components = [new ActionRowProperties([PendingModalInputService.BackButton(pendingId, lang), PendingModalInputService.CancelButton(pendingId, lang)])];
        };
    }

    // Whether this modal submit came from a component on one of our own ephemeral prompts (the Zurück
    // retry button) rather than the shared, persistent hub message. That's the deciding factor between
    // editing the prompt in place and posting a fresh ephemeral (CLAUDE.md's ModifyMessage-vs-new rule):
    // the first submit is opened from the hub button, every retry from the ephemeral's Zurück.
    private bool OpenedFromEphemeral =>
        Context.Interaction.Message is { } message && message.Flags.HasFlag(MessageFlags.Ephemeral);

    // location is read as a plain string, not a RaidServerLocation parameter — enum-from-
    // custom-id-string binding for component interactions isn't verified, unlike the
    // ulong/string positional binding already proven elsewhere, so this parses manually
    // instead of risking an unverified auto-conversion.
    [ComponentInteraction("raid-report-modal")]
    public Task ReportRaid(ulong targetUserId, string location) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var values = TextInputValues();
            var system = values.GetValueOrDefault("system") ?? "";
            var attacker = values.GetValueOrDefault("attacker");

            // Only the system lookup gets a retry step — an "already reported" rejection
            // (checked inside ReportRaidAsync) isn't something going back and retyping fixes.
            if (await alertService.FindSystemByNameAsync(system) is null)
            {
                var lang = await ActingUserLanguageAsync();
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.RaidReport,
                    targetUserId.ToString(), location, system, attacker);
                return await RetryEditAsync(Msg.Alert.UnknownSystem(lang, system), pendingId, lang);
            }

            // Confirm before firing. A raid alarm pings the whole alliance and the friends server,
            // and until now a typo'd system name or the wrong target went out the moment the modal
            // closed, with nothing between the mistake and everyone's phone. Legacy asked first.
            //
            // The values ride in a PendingModalInput rather than the custom id — a system name plus
            // an attacker name does not fit in 100 characters, and the retry path already stores
            // exactly this shape.
            var confirmLang = await ActingUserLanguageAsync();
            var pendingReportId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.RaidReport,
                targetUserId.ToString(), location, system, attacker);

            return await ConfirmEditAsync(pendingReportId, targetUserId, location, system, attacker, confirmLang);
        });

    // Everything the alarm will say, before it says it — plus what it will actually DO, which is the
    // part that differs: reporting yourself is a private rehearsal that only DMs you, and warning
    // about an alliance-wide alarm there would be a threat the bot has no intention of carrying out.
    private async Task<Action<MessageOptions>> ConfirmEditAsync(int pendingId, ulong targetUserId, string location, string system, string? attacker, Language lang)
    {
        var isTest = targetUserId == Context.User.Id;
        var summary = string.Join('\n',
            $"- {Msg.Bridge.RaidConfirmTarget(lang)}: <@{targetUserId}>",
            $"- {Msg.Bridge.RaidConfirmSystem(lang)}: {system}",
            $"- {Msg.Bridge.RaidConfirmAttacker(lang)}: {(string.IsNullOrWhiteSpace(attacker) ? Msg.Bridge.RaidConfirmUnspecified(lang) : attacker)}",
            $"- {Msg.Bridge.RaidConfirmServer(lang)}: {AlertService.ServerLocationLabel(lang, Enum.Parse<RaidServerLocation>(location))}");

        var body = Msg.Bridge.RaidConfirmIntro(lang, CommanderName.Of(Context.User), summary)
            + "\n\n" + (isTest ? Msg.Bridge.RaidConfirmTestNote(lang) : Msg.Bridge.RaidConfirmWarning(lang));

        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, body, title: Msg.Bridge.RaidModalTitle(lang));
        return m =>
        {
            m.Embeds = [embed];
            m.Components =
            [
                new ActionRowProperties(
                [
                    new ButtonProperties($"raid-report-confirm:{pendingId}", Msg.Bridge.RaidConfirmYes(lang), ButtonStyle.Danger),
                    new ButtonProperties($"raid-report-abort:{pendingId}", Msg.Bridge.RaidConfirmNo(lang), ButtonStyle.Primary),
                ]),
            ];
        };
    }

    [ComponentInteraction("raid-report-confirm")]
    public Task ConfirmRaidReport(int pendingId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            var lang = await ActingUserLanguageAsync();

            // Bound to the caller by GetAsync, so one member cannot confirm another's pending report.
            if (await pendingModalInputService.GetAsync(pendingId, Context.User.Id) is not { } pending)
                return await embedBranding.BrandedEditAsync(Context.Guild!.Id, Msg.Bridge.RaidConfirmExpired(lang));

            await pendingModalInputService.DeleteAsync(pendingId);

            var result = await alertService.ReportRaidAsync(Context.Guild!.Id, Context.User.Id, ulong.Parse(pending.Field1!),
                pending.Field3!, Enum.Parse<RaidServerLocation>(pending.Field2!),
                string.IsNullOrWhiteSpace(pending.Field4) ? null : pending.Field4);

            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, result);
        });

    [ComponentInteraction("raid-report-abort")]
    public Task AbortRaidReport(int pendingId) =>
        Context.Interaction.ModifyDelayedResponseAsync(async () =>
        {
            await pendingModalInputService.DeleteAsync(pendingId);
            return await embedBranding.BrandedEditAsync(Context.Guild!.Id, Msg.Bridge.RaidConfirmAborted(await ActingUserLanguageAsync()));
        });

    [ComponentInteraction("shield-reminder-setup-modal")]
    public Task SetShieldReminder()
    {
        async Task<Action<MessageOptions>> Work()
        {
            var values = TextInputValues();
            var duration = values.GetValueOrDefault("duration") ?? "";
            var system = values.GetValueOrDefault("system") ?? "";

            if (DurationParser.Parse(duration) is null)
            {
                var lang = await ActingUserLanguageAsync();
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                    duration, system);
                return await RetryEditAsync(Msg.Bridge.ShieldDurationParseError(lang), pendingId, lang);
            }

            var stfcSystem = await alertService.FindSystemByNameAsync(system);
            if (stfcSystem is null)
            {
                var lang = await ActingUserLanguageAsync();
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                    duration, system);
                return await RetryEditAsync(Msg.Alert.UnknownSystem(lang, system), pendingId, lang);
            }

            // A shield can only be parked in a housing system — a valid name without housing (e.g. Tezera
            // Beta) still can't hold one, so reject it with the same Zurück/Abbrechen retry.
            if (!stfcSystem.HasStationHousing)
            {
                var lang = await ActingUserLanguageAsync();
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                    duration, system);
                return await RetryEditAsync(AlertService.NoStationHousingMessage(lang, stfcSystem.Name), pendingId, lang);
            }

            var result = await alertService.SetShieldReminderAsync(Context.Guild!.Id, Context.User.Id, duration, system);
            // Present the confirmation as a branded embed like every other real bot message, not plain text.
            var confirmation = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, result);
            return m => { m.Content = ""; m.Embeds = [confirmation]; m.Components = []; };
        }

        // First submit is opened from the shared hub button → post a NEW ephemeral (never touch the
        // persistent hub message). Every retry is opened from the Zurück button on our ephemeral prompt
        // → edit that ephemeral in place instead of stacking a fresh one.
        return OpenedFromEphemeral
            ? Context.Interaction.ModifyDelayedResponseAsync(Work)
            : Context.Interaction.SendDelayedEditAsync(Work);
    }

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
