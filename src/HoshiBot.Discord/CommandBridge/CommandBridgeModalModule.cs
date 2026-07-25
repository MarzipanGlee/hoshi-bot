using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

public class CommandBridgeModalModule(AlertService alertService, PendingModalInputService pendingModalInputService, EmbedBranding embedBranding)
    : ComponentInteractionModule<ModalInteractionContext>
{
    // The "invalid input, try again" edit (retry/cancel buttons). Applied to whichever response
    // the caller acked: Raid edits its own ephemeral wizard message in place (ModifyDelayed),
    // while Shield Reminder — opened directly from the shared hub — acks a NEW ephemeral
    // (SendDelayedEdit) so this private prompt never lands on the public hub message. The embed
    // uses the same branded style as every real bot message, with a Zurück (reopen the modal,
    // pre-filled) / Abbrechen pair so a typo doesn't force restarting the whole flow.
    private async Task<Action<MessageOptions>> RetryEditAsync(string description, int pendingId)
    {
        var embed = await embedBranding.BuildBrandedAsync(Context.Guild!.Id, description, title: "Ungültige Eingabe");
        return m =>
        {
            // Clear the "⏳ Processing..." placeholder — otherwise it lingers as plain text above the
            // error embed (the ack was content-only; without this the edit only adds the embed).
            m.Content = "";
            m.Embeds = [embed];
            m.Components = [new ActionRowProperties([PendingModalInputService.BackButton(pendingId), PendingModalInputService.CancelButton(pendingId)])];
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
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.RaidReport,
                    targetUserId.ToString(), location, system, attacker);
                return await RetryEditAsync($"Unbekanntes System \"{system}\". Bitte die Schreibweise prüfen.", pendingId);
            }

            var serverLocation = Enum.Parse<RaidServerLocation>(location);

            var result = await alertService.ReportRaidAsync(Context.Guild!.Id, Context.User.Id, targetUserId,
                system, serverLocation, string.IsNullOrWhiteSpace(attacker) ? null : attacker);
            return m => { m.Content = result; m.Embeds = []; m.Components = []; };
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
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                    duration, system);
                return await RetryEditAsync("Konnte die Schildlaufzeit nicht lesen. Format z.B. \"2d3h45m\".", pendingId);
            }

            var stfcSystem = await alertService.FindSystemByNameAsync(system);
            if (stfcSystem is null)
            {
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                    duration, system);
                return await RetryEditAsync($"Unbekanntes System \"{system}\". Bitte die Schreibweise prüfen.", pendingId);
            }

            // A shield can only be parked in a housing system — a valid name without housing (e.g. Tezera
            // Beta) still can't hold one, so reject it with the same Zurück/Abbrechen retry.
            if (!stfcSystem.HasStationHousing)
            {
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                    duration, system);
                return await RetryEditAsync(AlertService.NoStationHousingMessage(stfcSystem.Name), pendingId);
            }

            var result = await alertService.SetShieldReminderAsync(Context.Guild!.Id, Context.User.Id, duration, system);
            return m => { m.Content = result; m.Embeds = []; m.Components = []; };
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
