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
            m.Embeds = [embed];
            m.Components = [new ActionRowProperties([PendingModalInputService.BackButton(pendingId), PendingModalInputService.CancelButton(pendingId)])];
        };
    }

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
    public Task SetShieldReminder() =>
        Context.Interaction.SendDelayedEditAsync(async () =>
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

            if (await alertService.FindSystemByNameAsync(system) is null)
            {
                var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                    duration, system);
                return await RetryEditAsync($"Unbekanntes System \"{system}\". Bitte die Schreibweise prüfen.", pendingId);
            }

            var result = await alertService.SetShieldReminderAsync(Context.Guild!.Id, Context.User.Id, duration, system);
            return m => { m.Content = result; m.Embeds = []; m.Components = []; };
        });

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
