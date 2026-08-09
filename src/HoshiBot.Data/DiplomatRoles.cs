using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Who handles this alliance's diplomacy — today, the role pinged when a RoE case is marked ready.
//
// The Diplomacy feature is settings-only: there is no module, no /set-diplomacy command, and nothing
// in the bot reads StfcAllianceDiplomacy. Its editor still shows this role because it is the feature
// the role belongs to conceptually and will use once built.
//
// It lived in the Diplomacy feature's own settings, which made RoE Violation Reports reach across
// and read another feature's key to render its picker; that page's help text had to say "this is
// the same Diplomat role as the Diplomacy feature" because nothing else conveyed it. Two features
// reading one role is the definition of a shared setting, so it belongs to the alliance — the same
// move Senior Staff made, for the same reason. See SeniorStaffRoles.
//
// Alliance only, deliberately: both features that read it are Alliance-audience, so a
// GuildAudienceSettings column would be a column nothing could ever fill. Add one if a
// non-alliance feature ever needs a diplomat.
public class DiplomatRoles(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<ulong?> ForScopeAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        if (audience != GuildAudience.Alliance || guildAllianceId is not { } allianceId)
            return null;

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildAlliances
            .Where(a => a.GuildId == guildId && a.Id == allianceId)
            .Select(a => a.DiplomatRoleId)
            .FirstOrDefaultAsync();
    }

    public Task<ulong?> ForAllianceAsync(ulong guildId, int guildAllianceId) =>
        ForScopeAsync(guildId, GuildAudience.Alliance, guildAllianceId);
}
