using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord;

public class TerritoryCaptureButtonModule(HoshiBotDbContext db) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("territory-capture-unsubscribe")]
    public Task Unsubscribe(int territoryId, long startUnix, long endUnix) =>
        Context.Interaction.SendDelayedResponseAsync(async () =>
        {
            var guildId = Context.Guild!.Id;
            var userId = Context.User.Id;
            var start = DateTimeOffset.FromUnixTimeSeconds(startUnix);
            var end = DateTimeOffset.FromUnixTimeSeconds(endUnix);

            var overlapping = await db.Absences
                .AnyAsync(a => a.GuildId == guildId && a.DiscordUserId == userId
                    && a.StartsAt < end && a.EndsAt > start);
            if (overlapping)
                return CommanderName.Address(Context.User, "Du hast für diesen Zeitraum bereits eine Abwesenheit erfasst.");

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
                Reason = territory is null ? "Abmeldung Gebietsübernahme" : $"Abmeldung für {territory.Name}",
                SuppressNotifications = false,
                CreatedByDiscordUserId = userId,
            });

            await db.SaveChangesAsync();

            return CommanderName.Address(Context.User, "Deine Abwesenheit wurde erfasst. Besten Dank für Deine Meldung!");
        });
}
