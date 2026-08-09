using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// The two roles Boarding moves a member between, per scope: the temporary one they hold until they
// confirm, and the one that means they are in.
//
// Same shape as SeniorStaffRoles/DiplomatRoles/AlertRoles — the Alliance audience reads GuildAlliance,
// every other audience reads GuildAudienceSettings. Both roles live in one class because Boarding is
// the only reader of either and always needs the pair: a scope with one but not the other cannot
// board anybody, and the editor refuses to post until both are set.
public class BoardingRoles(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public record ScopeRoles(ulong? MemberRoleId, ulong? BoardingRoleId)
    {
        // Boarding is only configurable when it can complete the round trip.
        public bool Complete => MemberRoleId is not null && BoardingRoleId is not null;
    }

    public async Task<ScopeRoles> ForScopeAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (audience == GuildAudience.Alliance && guildAllianceId is { } allianceId)
        {
            return await db.GuildAlliances
                .Where(a => a.GuildId == guildId && a.Id == allianceId)
                .Select(a => new ScopeRoles(a.MemberRoleId, a.BoardingRoleId))
                .FirstOrDefaultAsync() ?? new ScopeRoles(null, null);
        }

        return await db.GuildAudienceSettings
            .Where(a => a.GuildId == guildId && a.Audience == audience)
            .Select(a => new ScopeRoles(a.MemberRoleId, a.BoardingRoleId))
            .FirstOrDefaultAsync() ?? new ScopeRoles(null, null);
    }
}
