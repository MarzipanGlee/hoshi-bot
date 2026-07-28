using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Alerts;

public class AlertModule(AlertService alertService, HoshiBotDbContext db, GuildFeatureService featureService, EmbedBranding embedBranding) : ApplicationCommandModule<ApplicationCommandContext>
{
    // Catalog-rendered strings (the feature-disabled guard) are pinned to German until
    // sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    // Tip: targeting yourself runs a self-test — see AlertService.ReportRaidAsync.
    [SlashCommand("raid", "Report a raid on a commander's station (tip: target yourself to try it out risk-free)", Contexts = [InteractionContextType.Guild])]
    public Task ReportRaid(
        User target,
        [SlashCommandParameter(AutocompleteProviderType = typeof(StationHousingSystemAutocompleteProvider))] string system,
        RaidServerLocation server,
        string? attacker = null) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.RaidAlerts, Lang) is { } msg)
                return msg;

            return await alertService.ReportRaidAsync(Context.Guild!.Id, Context.User.Id, target.Id, system, server, attacker);
        });

    // Not feature-gated, unlike ReportRaid — disabling RaidAlerts should stop new reports,
    // not strand an already-active alert with no way to end it (matches the equivalent
    // "Beenden" button on the notification itself, which is likewise never gated).
    [SlashCommand("raid-terminate", "End an active raid alert", Contexts = [InteractionContextType.Guild])]
    public Task TerminateRaid(User? target = null) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, () => alertService.TerminateRaidAsync(Context.Guild!.Id, Context.User.Id, target?.Id ?? Context.User.Id));

    [SlashCommand("shield-reminder", "Set a reminder for when your shield expires", Contexts = [InteractionContextType.Guild])]
    public Task SetShieldReminder(
        string duration,
        [SlashCommandParameter(AutocompleteProviderType = typeof(StationHousingSystemAutocompleteProvider))] string system) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders, Lang) is { } msg)
                return msg;

            return await alertService.SetShieldReminderAsync(Context.Guild!.Id, Context.User.Id, duration, system);
        });

    // Not feature-gated — same reasoning as TerminateRaid above: a member must always be
    // able to remove their own existing reminder, even after an admin disables the feature.
    [SlashCommand("shield-reminder-remove", "Remove your shield reminder", Contexts = [InteractionContextType.Guild])]
    public Task RemoveShieldReminder() =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, () => alertService.TerminateShieldReminderAsync(Context.Guild!.Id, Context.User.Id));

    // Not feature-gated — same reasoning as TerminateRaid above.
    [SlashCommand("shield-reminder-disable", "Permanently disable shield reminders for yourself", Contexts = [InteractionContextType.Guild])]
    public Task DisableShieldReminder() =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;
            var userId = Context.User.Id;

            var reminder = await db.ShieldReminders.FirstOrDefaultAsync(s => s.GuildId == guildId && s.DiscordUserId == userId);
            if (reminder is null)
                return "You don't have a shield reminder set.";

            reminder.Disabled = true;
            await db.SaveChangesAsync();

            return "Shield reminders disabled. Run /shield-reminder again anytime to re-enable.";
        });
}
