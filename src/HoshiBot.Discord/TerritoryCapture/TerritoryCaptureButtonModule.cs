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
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("territory-capture-unsubscribe")]
    public Task Unsubscribe(int territoryId, long startUnix, long endUnix) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;
            var userId = Context.User.Id;

            // The confirmation is ephemeral to the clicking member, so it renders in their language.
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

            // The reason is NOT the clicking member's language: it's stored verbatim and read back
            // out on the alliance's public absence report, which renders in the alliance language.
            // A German alliance was getting "Sign-off for Tigan" among its "Abmeldung für …" rows,
            // purely because that member's own client is English.
            var reasonLang = await SignOffLanguageAsync(guildId);

            db.Absences.Add(new Absence
            {
                GuildId = guildId,
                DiscordUserId = userId,
                StartsAt = start,
                EndsAt = end,
                Reason = territory is null ? Msg.Tc.AbsenceReasonGeneric(reasonLang) : Msg.Tc.AbsenceReason(reasonLang, territory.Name),
                SuppressNotifications = false,
                CreatedByDiscordUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();

            return Msg.Tc.AbsenceRecorded(lang, CommanderName.Of(Context.User));
        });

    // Which alliance's language the stored reason renders in. The button's custom id predates this
    // and carries no alliance, so the alliance is recovered from the channel the digest was posted
    // to — every sign-off button lives in that alliance's TerritoryCapture DigestChannel. Same
    // channel-to-alliance recovery the staff bridge does, and it works for buttons already posted.
    //
    // Falls back to the guild language when the channel can't be matched (the setting was repointed
    // after the digest went out): still a scope language, never the clicking member's.
    private async Task<Language> SignOffLanguageAsync(ulong guildId)
    {
        var scopes = await settingsService.FindScopesByValueAsync(
            guildId, GuildFeature.TerritoryCapture, TerritoryCaptureSettingKeys.DigestChannel, Context.Channel.Id);

        var guildAllianceId = scopes
            .Where(s => s.Audience == GuildAudience.Alliance)
            .Select(s => s.GuildAllianceId)
            .FirstOrDefault(id => id is not null);

        return guildAllianceId is { } allianceId
            ? await languageResolver.ForAllianceAsync(allianceId)
            : await languageResolver.ForGuildAsync(guildId);
    }
}
