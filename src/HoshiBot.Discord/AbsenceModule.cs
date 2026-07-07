using HoshiBot.Data;
using HoshiBot.Discord.Absences;
using HoshiBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord;

// Kept alongside the Command Bridge button/modal flow (AbsenceButtonModule etc.) —
// both are valid entry points to the same AbsenceService logic. Saves straight to
// Confirmed (no draft/confirm step): the command's arguments are already the review.
public class AbsenceModule(AbsenceService absenceService, GuildFeatureService featureService) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("absence", "Report yourself as absent for a number of hours",
        Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> ReportAbsence(
        double hours,
        string? reason = null,
        bool suppressNotifications = true,
        AbsenceVisibility visibility = AbsenceVisibility.Public)
    {
        if (!await featureService.IsEnabledAsync(Context.Guild!.Id, GuildFeature.Absences))
            return EphemeralReply.Of(GuildFeatureService.DisabledMessage(GuildFeature.Absences));

        if (hours <= 0)
            return EphemeralReply.Of("Hours must be greater than 0.");

        var now = DateTimeOffset.UtcNow;
        var endsAt = now.AddHours(hours);

        await absenceService.CreateAsync(Context.Guild!.Id, Context.User.Id, now, endsAt,
            reason, visibility, suppressNotifications);

        return EphemeralReply.Of($"Absence recorded until <t:{endsAt.ToUnixTimeSeconds()}:f>."
            + (suppressNotifications ? " You'll be excluded from notifications until then." : ""));
    }
}
