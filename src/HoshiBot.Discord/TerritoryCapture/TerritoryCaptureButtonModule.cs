using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace HoshiBot.Discord.TerritoryCapture;

public class TerritoryCaptureButtonModule(HoshiBotDbContext db, EmbedBranding embedBranding) : ComponentInteractionModule<ButtonInteractionContext>
{
    // All strings come from the message catalog (Msg.Tc); rendering is pinned to German
    // until sub-phase 6e wires up per-scope language resolution (docs/localization-plan.md).
    private const Language Lang = Language.De;

    [ComponentInteraction("territory-capture-unsubscribe")]
    public Task Unsubscribe(int territoryId, long startUnix, long endUnix) =>
        Context.Interaction.SendDelayedEmbedAsync(embedBranding, Context.Guild!.Id, async () =>
        {
            var guildId = Context.Guild!.Id;
            var userId = Context.User.Id;
            var start = DateTimeOffset.FromUnixTimeSeconds(startUnix);
            var end = DateTimeOffset.FromUnixTimeSeconds(endUnix);

            var overlapping = await db.Absences
                .AnyAsync(a => a.GuildId == guildId && a.DiscordUserId == userId
                    && a.StartsAt < end && a.EndsAt > start);
            if (overlapping)
                return Msg.Tc.AlreadyAbsent(Lang, CommanderName.Of(Context.User));

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
                Reason = territory is null ? Msg.Tc.AbsenceReasonGeneric(Lang) : Msg.Tc.AbsenceReason(Lang, territory.Name),
                SuppressNotifications = false,
                CreatedByDiscordUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();

            return Msg.Tc.AbsenceRecorded(Lang, CommanderName.Of(Context.User));
        });
}
