using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.CommandBridge;

public class CommandBridgeModalModule(AlertService alertService, PendingModalInputService pendingModalInputService, EmbedBranding embedBranding)
    : ComponentInteractionModule<ModalInteractionContext>
{
    // Shared shape for the "your input didn't validate" step — same branded style as
    // every real bot message, with a Zurück (reopen the modal, pre-filled) / Abbrechen
    // pair so a typo doesn't force restarting the whole flow.
    private async Task<EmbedProperties> RetryEmbedAsync(string description)
    {
        var guildId = Context.Guild!.Id;
        return new EmbedProperties
        {
            Title = "Ungültige Eingabe",
            Description = description,
            Color = EmbedBranding.BotColor,
            Author = await embedBranding.BuildAuthorAsync(guildId),
            Footer = embedBranding.BuildFooter(guildId),
        };
    }

    // Used when the modal was opened from our own ephemeral wizard message (Raid's flow
    // always is) — safe to edit that message in place.
    private async Task<InteractionCallbackProperties<MessageOptions>> RetryModifyMessageAsync(string description, int pendingId)
    {
        var embed = await RetryEmbedAsync(description);
        return InteractionCallback.ModifyMessage(m =>
        {
            m.Embeds = [embed];
            m.Components = [new ActionRowProperties([PendingModalInputService.BackButton(pendingId), PendingModalInputService.CancelButton(pendingId)])];
        });
    }

    // Used when the modal was opened directly from the public, shared Command Bridge hub
    // message (Shield Reminder's flow) — editing that message would leak this user's
    // private retry prompt into a message everyone else can see, so this must stay a new
    // ephemeral message instead.
    private async Task<InteractionMessageProperties> RetryNewMessageAsync(string description, int pendingId)
    {
        var embed = await RetryEmbedAsync(description);
        return new InteractionMessageProperties
        {
            Embeds = [embed],
            Flags = MessageFlags.Ephemeral,
            Components = [new ActionRowProperties([PendingModalInputService.BackButton(pendingId), PendingModalInputService.CancelButton(pendingId)])],
        };
    }

    // location is read as a plain string, not a RaidServerLocation parameter — enum-from-
    // custom-id-string binding for component interactions isn't verified, unlike the
    // ulong/string positional binding already proven elsewhere, so this parses manually
    // instead of risking an unverified auto-conversion.
    [ComponentInteraction("raid-report-modal")]
    public async Task<InteractionCallbackProperties<MessageOptions>> ReportRaid(ulong targetUserId, string location)
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
            return await RetryModifyMessageAsync($"Unbekanntes System \"{system}\". Bitte die Schreibweise prüfen.", pendingId);
        }

        var serverLocation = Enum.Parse<RaidServerLocation>(location);

        var result = await alertService.ReportRaidAsync(Context.Guild!.Id, Context.User.Id, targetUserId,
            system, serverLocation, string.IsNullOrWhiteSpace(attacker) ? null : attacker);
        return InteractionCallback.ModifyMessage(m => { m.Content = result; m.Embeds = []; m.Components = []; });
    }

    [ComponentInteraction("shield-reminder-setup-modal")]
    public async Task<InteractionMessageProperties> SetShieldReminder()
    {
        var values = TextInputValues();
        var duration = values.GetValueOrDefault("duration") ?? "";
        var system = values.GetValueOrDefault("system") ?? "";

        if (DurationParser.Parse(duration) is null)
        {
            var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                duration, system);
            return await RetryNewMessageAsync("Konnte die Schildlaufzeit nicht lesen. Format z.B. \"2d3h45m\".", pendingId);
        }

        if (await alertService.FindSystemByNameAsync(system) is null)
        {
            var pendingId = await pendingModalInputService.CreateAsync(Context.Guild!.Id, Context.User.Id, PendingModalInputKind.ShieldReminder,
                duration, system);
            return await RetryNewMessageAsync($"Unbekanntes System \"{system}\". Bitte die Schreibweise prüfen.", pendingId);
        }

        return EphemeralReply.Of(await alertService.SetShieldReminderAsync(Context.Guild!.Id, Context.User.Id, duration, system));
    }

    private Dictionary<string, string> TextInputValues() =>
        Context.Components
            .OfType<Label>()
            .Select(l => l.Component)
            .OfType<TextInput>()
            .ToDictionary(i => i.CustomId, i => i.Value);
}
