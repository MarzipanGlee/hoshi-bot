using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

public class AlertModule(AlertService alertService, HoshiBotDbContext db, GuildFeatureService featureService) : ApplicationCommandModule<ApplicationCommandContext>
{
    // Tip: targeting yourself runs a self-test — see AlertService.ReportRaidAsync.
    [SlashCommand("raid", "Report a raid on a commander's station (tip: target yourself to try it out risk-free)", Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> ReportRaid(
        User target,
        [SlashCommandParameter(AutocompleteProviderType = typeof(StationHousingSystemAutocompleteProvider))] string system,
        RaidServerLocation server,
        string? attacker = null)
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.RaidAlerts))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.RaidAlerts));

        return EphemeralReply.Of(await alertService.ReportRaidAsync(Context.Guild!.Id, Context.User.Id, target.Id, system, server, attacker));
    }

    // Not feature-gated, unlike ReportRaid — disabling RaidAlerts should stop new reports,
    // not strand an already-active alert with no way to end it (matches the equivalent
    // "Beenden" button on the notification itself, which is likewise never gated).
    [SlashCommand("raid-terminate", "End an active raid alert", Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> TerminateRaid(User? target = null) =>
        EphemeralReply.Of(await alertService.TerminateRaidAsync(Context.Guild!.Id, Context.User.Id, target?.Id ?? Context.User.Id));

    [SlashCommand("shield-reminder", "Set a reminder for when your shield expires", Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> SetShieldReminder(
        string duration,
        [SlashCommandParameter(AutocompleteProviderType = typeof(StationHousingSystemAutocompleteProvider))] string system)
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.ShieldReminders))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.ShieldReminders));

        return EphemeralReply.Of(await alertService.SetShieldReminderAsync(Context.Guild!.Id, Context.User.Id, duration, system));
    }

    // Not feature-gated — same reasoning as TerminateRaid above: a member must always be
    // able to remove their own existing reminder, even after an admin disables the feature.
    [SlashCommand("shield-reminder-remove", "Remove your shield reminder", Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> RemoveShieldReminder() =>
        EphemeralReply.Of(await alertService.TerminateShieldReminderAsync(Context.Guild!.Id, Context.User.Id));

    // Not feature-gated — same reasoning as TerminateRaid above.
    [SlashCommand("shield-reminder-disable", "Permanently disable shield reminders for yourself", Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> DisableShieldReminder()
    {
        var guildId = Context.Guild!.Id;
        var userId = Context.User.Id;

        var reminder = await db.ShieldReminders.FirstOrDefaultAsync(s => s.GuildId == guildId && s.DiscordUserId == userId);
        if (reminder is null)
            return EphemeralReply.Of("You don't have a shield reminder set.");

        reminder.Disabled = true;
        await db.SaveChangesAsync();

        return EphemeralReply.Of("Shield reminders disabled. Run /shield-reminder again anytime to re-enable.");
    }
}
