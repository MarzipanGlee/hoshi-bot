using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// The role an alliance's raid and shield alerts ping, and the one members toggle in the Notification
// Opt-In menu — necessarily the same role, which is why it is one setting.
//
// It used to be two: Notification Opt-In owned what members could toggle, while each alert-channel
// row owned what an alert actually mentioned. Nothing compared them, so a channel could ping a role
// no member was ever offered. On the test guild one raid channel already did.
//
// Alliance only: raid alerts, shield reminders and the opt-in menu are all Alliance-audience, so a
// GuildAudienceSettings column would be one nothing could fill. Same call as DiplomatRoles.
public class AlertRoles(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<ulong?> ForScopeAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        if (audience != GuildAudience.Alliance || guildAllianceId is not { } allianceId)
            return null;

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildAlliances
            .Where(a => a.GuildId == guildId && a.Id == allianceId)
            .Select(a => a.AlertRoleId)
            .FirstOrDefaultAsync();
    }

    public Task<ulong?> ForAllianceAsync(ulong guildId, int guildAllianceId) =>
        ForScopeAsync(guildId, GuildAudience.Alliance, guildAllianceId);
}
