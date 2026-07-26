using HoshiBot.Data;
using HoshiBot.Discord.Absences;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Absences;

// Kept alongside the Command Bridge button/modal flow (AbsenceButtonModule etc.) —
// both are valid entry points to the same AbsenceService logic. Saves straight to
// Confirmed (no draft/confirm step): the command's arguments are already the review.
public class AbsenceModule(AbsenceService absenceService, GuildFeatureService featureService, EmbedBranding embedBranding) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("absence", "Report yourself as absent for a number of hours",
        Contexts = [InteractionContextType.Guild])]
    public Task ReportAbsence(
        double hours,
        string? reason = null,
        bool suppressNotifications = true,
        AbsenceVisibility visibility = AbsenceVisibility.Public) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.Absences) is { } msg)
                return msg;

            if (hours <= 0)
                return "Hours must be greater than 0.";

            var now = DateTimeOffset.UtcNow;
            var endsAt = now.AddHours(hours);

            await absenceService.CreateAsync(Context.Guild!.Id, Context.User.Id, now, endsAt,
                reason, visibility, suppressNotifications);

            return $"Absence recorded until <t:{endsAt.ToUnixTimeSeconds()}:f>."
                + (suppressNotifications ? " You'll be excluded from notifications until then." : "");
        });
}
