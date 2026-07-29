using HoshiBot.Data;
using HoshiBot.Discord.Absences;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace HoshiBot.Discord.Absences;

// Kept alongside the Command Bridge button/modal flow (AbsenceButtonModule etc.) —
// both are valid entry points to the same AbsenceService logic. Saves straight to
// Confirmed (no draft/confirm step): the command's arguments are already the review.
public class AbsenceModule(AbsenceService absenceService, GuildFeatureService featureService, EmbedBranding embedBranding, LanguageResolver languageResolver) : ApplicationCommandModule<ApplicationCommandContext>
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
            // The whole reply goes back to the reporting user alone.
            var lang = await languageResolver.ForUserAsync(Context.User.Id, Context.Interaction.UserLocale, Context.Guild!.Id);

            if (await featureService.EnsureEnabledAsync(Context.Guild!.Id, GuildFeature.Absences, lang) is { } msg)
                return msg;

            if (hours <= 0)
                return Msg.Absence.HoursMustBePositive(lang);

            var now = DateTimeOffset.UtcNow;
            var endsAt = now.AddHours(hours);

            await absenceService.CreateAsync(Context.Guild!.Id, Context.User.Id, now, endsAt,
                reason, visibility, suppressNotifications);

            return Msg.Absence.Recorded(lang, endsAt.ToUnixTimeSeconds())
                + (suppressNotifications ? Msg.Absence.RecordedNotifySuffix(lang) : "");
        });
}
