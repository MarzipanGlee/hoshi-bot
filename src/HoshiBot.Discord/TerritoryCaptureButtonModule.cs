using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord;

public class TerritoryCaptureButtonModule(HoshiBotDbContext db) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("territory-capture-unsubscribe")]
    public async Task<InteractionMessageProperties> Unsubscribe(int territoryId, long startUnix, long endUnix)
    {
        var guildId = Context.Guild!.Id;
        var userId = Context.User.Id;
        var start = DateTimeOffset.FromUnixTimeSeconds(startUnix);
        var end = DateTimeOffset.FromUnixTimeSeconds(endUnix);

        // Filtered client-side: SQLite's EF Core provider can't translate DateTimeOffset
        // range comparisons here, and per-user absence counts are always small.
        var overlapping = (await db.Absences
            .Where(a => a.GuildId == guildId && a.DiscordUserId == userId)
            .ToListAsync())
            .Any(a => a.StartsAt < end && a.EndsAt > start);
        if (overlapping)
            return EphemeralReply.Of("Du hast für diesen Zeitraum bereits eine Abwesenheit erfasst.");

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

        return EphemeralReply.Of("Deine Abwesenheit wurde erfasst. Besten Dank für Deine Meldung!");
    }
}
