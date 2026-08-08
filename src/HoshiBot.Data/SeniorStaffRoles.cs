using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Who counts as senior staff, per scope — Star Trek's term for a ship's leadership body, and the
// bot's name for the members allowed to act on an alliance's behalf. Distinct from RANK, which
// StfcPlayerRank and the Rank Roles feature already model from the five in-game tiers: holding a
// tier and being allowed to act for the alliance are different questions.
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
//
// A guild that genuinely wants ONE shared leadership needs nothing added here: Conditional Roles
// already grants a role while a boolean expression over a member's other roles holds, so "holds the
// shared staff role -> also gets each alliance's staff role" is a rule an admin writes. That keeps
// the per-scope value honest (every alliance still names its own staff role, and the attribution
// line still reads correctly) while making the shared case a configuration, not a second concept
// here.
public class SeniorStaffRoles(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    public async Task<ulong?> ForScopeAsync(ulong guildId, GuildAudience audience, int? guildAllianceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (audience == GuildAudience.Alliance && guildAllianceId is { } allianceId)
        {
            return await db.GuildAlliances
                .Where(a => a.GuildId == guildId && a.Id == allianceId)
                .Select(a => a.SeniorStaffRoleId)
                .FirstOrDefaultAsync();
        }

        return await db.GuildAudienceSettings
            .Where(a => a.GuildId == guildId && a.Audience == audience)
            .Select(a => a.SeniorStaffRoleId)
            .FirstOrDefaultAsync();
    }

    public async Task<HashSet<ulong>> AllForGuildAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var fromAlliances = await db.GuildAlliances
            .Where(a => a.GuildId == guildId && a.SeniorStaffRoleId != null)
            .Select(a => a.SeniorStaffRoleId!.Value)
            .ToListAsync();

        var fromAudiences = await db.GuildAudienceSettings
            .Where(a => a.GuildId == guildId && a.SeniorStaffRoleId != null)
            .Select(a => a.SeniorStaffRoleId!.Value)
            .ToListAsync();

        return [.. fromAlliances, .. fromAudiences];
    }

    public async Task<bool> IsSeniorStaffAsync(ulong guildId, IEnumerable<ulong> memberRoleIds)
    {
        var staffRoles = await AllForGuildAsync(guildId);
        return staffRoles.Count > 0 && memberRoleIds.Any(staffRoles.Contains);
    }
}
