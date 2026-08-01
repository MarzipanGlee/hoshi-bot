using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.TerritoryCapture;

public class TerritoryCaptureButtonModule(
    HoshiBotDbContext db,
    EmbedBranding embedBranding,
    LanguageResolver languageResolver,
    GuildFeatureService featureService) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("territory-capture-unsubscribe")]
    public Task Unsubscribe(int territoryId, long startUnix, long endUnix) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;
            var userId = Context.User.Id;

            // The confirmation is ephemeral to the clicking member — their language (also used
            // for the stored absence reason: it's their own absence row).
            var lang = await languageResolver.ForUserAsync(userId, Context.Interaction.UserLocale, guildId);

            // New digests/reminders stop rendering the button as soon as either feature goes off,
            // but already-posted ones stay clickable for their whole retention window — so guard
            // both here as well. Absences matters most: this writes an Absence row directly, and
            // without that feature the member could never see, edit or delete what they created
            // and no report would ever read it.
            if (await featureService.EnsureEnabledAsync(guildId, GuildFeature.TerritoryCaptureSignOff, lang) is { } signOffDisabled)
                return signOffDisabled;
            if (await featureService.EnsureEnabledAsync(guildId, GuildFeature.Absences, lang) is { } absencesDisabled)
                return absencesDisabled;

            var start = DateTimeOffset.FromUnixTimeSeconds(startUnix);
            var end = DateTimeOffset.FromUnixTimeSeconds(endUnix);

            var overlapping = await db.Absences
                .AnyAsync(a => a.GuildId == guildId && a.DiscordUserId == userId
                    && a.StartsAt < end && a.EndsAt > start);
            if (overlapping)
                return Msg.Tc.AlreadyAbsent(lang, CommanderName.Of(Context.User));

            if (await db.DiscordUsers.FindAsync(userId) is null)
                db.DiscordUsers.Add(new DiscordUser { DiscordUserId = userId });
            if (await db.GuildMembers.FindAsync(guildId, userId) is null)
                db.GuildMembers.Add(new GuildMember { GuildId = guildId, DiscordUserId = userId, JoinedAt = DateTimeOffset.UtcNow });

            var territory = await db.StfcTerritories.FindAsync(territoryId);

            db.Absences.Add(new Absence
            {
                GuildId = guildId,
                DiscordUserId = userId,
                StartsAt = start,
                EndsAt = end,
                Reason = territory is null ? Msg.Tc.AbsenceReasonGeneric(lang) : Msg.Tc.AbsenceReason(lang, territory.Name),
                SuppressNotifications = false,
                CreatedByDiscordUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();

            return Msg.Tc.AbsenceRecorded(lang, CommanderName.Of(Context.User));
        });
}
