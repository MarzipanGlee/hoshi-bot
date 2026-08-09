using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// "You belong here", per scope — the role an alliance, server, veil group or community gives the
// people who are actually theirs.
//
// Its own class rather than a field on Boarding's, because two features read it for different
// reasons: Boarding grants it when someone confirms the welcome message, and Member Lore uses it to
// decide who is worth interviewing. Same shape as SeniorStaffRoles/DiplomatRoles/AlertRoles.
public class MemberRoles(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<ulong?> ForScopeAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (audience == GuildAudience.Alliance && guildAllianceId is { } allianceId)
        {
            return await db.GuildAlliances
                .Where(a => a.GuildId == guildId && a.Id == allianceId)
                .Select(a => a.MemberRoleId)
                .FirstOrDefaultAsync();
        }

        return await db.GuildAudienceSettings
            .Where(a => a.GuildId == guildId && a.Audience == audience)
            .Select(a => a.MemberRoleId)
            .FirstOrDefaultAsync();
    }
}
