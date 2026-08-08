using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Who counts as command staff, per scope.
//
// It used to be one GuildSettings role for the whole Discord, which meant a coalition guild gave
// every linked alliance the same leadership — one alliance's staff could end another's raid alerts
// and sign another's announcements. It lives on GuildAlliance now (and GuildAudienceSettings for the
// audiences that have no alliance), which is where the rest of a scope's roles already live.
//
// Two questions, deliberately different:
//
//   ForScopeAsync — "whose staff signs THIS post". Announcements needs the specific role because it
//   renders its name in the attribution line.
//
//   AllForGuildAsync — "is this member staff at all". The permission gates (reporting on behalf of
//   an own player, ending another commander's raid alert) only ask that, and asking per alliance
//   would mean a coalition's staff could not help each other — a stricter rule than the one that was
//   there before, imposed by a refactor rather than chosen.
public class CommandStaffRoles(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<ulong?> ForScopeAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (audience == GuildAudience.Alliance && guildAllianceId is { } allianceId)
        {
            return await db.GuildAlliances
                .Where(a => a.GuildId == guildId && a.Id == allianceId)
                .Select(a => a.CommandStaffRoleId)
                .FirstOrDefaultAsync();
        }

        return await db.GuildAudienceSettings
            .Where(a => a.GuildId == guildId && a.Audience == audience)
            .Select(a => a.CommandStaffRoleId)
            .FirstOrDefaultAsync();
    }

    public async Task<HashSet<ulong>> AllForGuildAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var fromAlliances = await db.GuildAlliances
            .Where(a => a.GuildId == guildId && a.CommandStaffRoleId != null)
            .Select(a => a.CommandStaffRoleId!.Value)
            .ToListAsync();

        var fromAudiences = await db.GuildAudienceSettings
            .Where(a => a.GuildId == guildId && a.CommandStaffRoleId != null)
            .Select(a => a.CommandStaffRoleId!.Value)
            .ToListAsync();

        return [.. fromAlliances, .. fromAudiences];
    }

    public async Task<bool> IsCommandStaffAsync(ulong guildId, IEnumerable<ulong> memberRoleIds)
    {
        var staffRoles = await AllForGuildAsync(guildId);
        return staffRoles.Count > 0 && memberRoleIds.Any(staffRoles.Contains);
    }
}
