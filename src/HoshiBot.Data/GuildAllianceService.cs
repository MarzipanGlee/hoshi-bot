using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Resolves which alliance(s) a guild has linked, and which one a given context (a route param,
// a Discord member, or "the primary/default") maps to. Central to per-alliance feature scoping:
// GuildEnabledFeature/GuildFeatureSetting* rows for the Alliance audience are keyed by a
// GuildAlliance.Id, and everything that reads or writes them needs to turn "this guild" into
// "this specific linked alliance" first.
//
// "Primary" alliance = the lowest-Id link, which is also where the AddGuildAllianceScope
// migration backfilled the pre-multi-alliance settings — so treating the primary as the default
// keeps single-alliance guilds behaving exactly as before.
public class GuildAllianceService(IDbContextFactory<HoshiBotDbContext> dbFactory, PlayerLinkService playerLinkService)
{
    public async Task<List<GuildAlliance>> GetLinksAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildAlliances
            .Include(ga => ga.StfcAlliance).ThenInclude(a => a.Server).ThenInclude(s => s.Region)
            .Where(ga => ga.GuildId == guildId)
            .OrderBy(ga => ga.Id)
            .ToListAsync();
    }

    public async Task<GuildAlliance?> GetPrimaryAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildAlliances
            .Include(ga => ga.StfcAlliance)
            .Where(ga => ga.GuildId == guildId)
            .OrderBy(ga => ga.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<int?> GetPrimaryIdAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildAlliances
            .Where(ga => ga.GuildId == guildId)
            .OrderBy(ga => ga.Id)
            .Select(ga => (int?)ga.Id)
            .FirstOrDefaultAsync();
    }

    // Resolves the feature scope a component custom-id's audience segment maps to: parses the
    // GuildAudience and, for the Alliance audience, resolves the guild's primary linked alliance
    // (Phase 1 — every other audience is guild-scoped, null). AllianceMissing is true only when
    // the Alliance audience has no linked alliance, so callers can treat that scope as disabled.
    public async Task<(GuildAudience Audience, int? GuildAllianceId, bool AllianceMissing)> ResolveScopeAsync(ulong guildId, string audience)
    {
        var parsed = Enum.Parse<GuildAudience>(audience);
        if (parsed != GuildAudience.Alliance)
            return (parsed, null, false);

        var primary = await GetPrimaryIdAsync(guildId);
        return (parsed, primary, primary is null);
    }

    // Ownership-validated lookup: returns the link only if it actually belongs to this guild
    // (guards against a route param / custom-id carrying another guild's alliance id).
    public async Task<GuildAlliance?> FindByIdAsync(ulong guildId, int guildAllianceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.GuildAlliances
            .Include(ga => ga.StfcAlliance)
            .FirstOrDefaultAsync(ga => ga.GuildId == guildId && ga.Id == guildAllianceId);
    }

    // Which of the guild's linked alliances a Discord member belongs to, via the in-game alliance of
    // the player that represents them *in this guild* (GuildMember.PrimaryStfcPlayerId, falling back
    // to their oldest link). Null when the member has no linked player or that player's alliance
    // isn't one the guild links — callers typically fall back to the primary alliance.
    public async Task<GuildAlliance?> FindByMemberAsync(ulong guildId, ulong discordUserId)
    {
        var playerId = await playerLinkService.GetGuildPrimaryPlayerIdAsync(guildId, discordUserId);
        if (playerId is null)
            return null;

        await using var db = await dbFactory.CreateDbContextAsync();
        var allianceId = await db.StfcPlayers
            .Where(p => p.Id == playerId)
            .Select(p => p.AllianceId)
            .FirstOrDefaultAsync();
        if (allianceId is null)
            return null;

        return await db.GuildAlliances
            .Include(ga => ga.StfcAlliance)
            .FirstOrDefaultAsync(ga => ga.GuildId == guildId && ga.StfcAllianceId == allianceId);
    }

    // Self-heal: assign any Alliance-audience feature rows still left with a null GuildAllianceId
    // (seeded before the guild linked an alliance, or migrated in a guild that had no links yet)
    // to the given alliance. Called at startup and when a guild links its first alliance, so
    // pre-existing Alliance settings become readable under the per-alliance model rather than
    // silently stranded. Returns the number of rows adopted.
    public async Task<int> AdoptOrphansAsync(ulong guildId, int guildAllianceId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var enabled = await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId && f.Audience == GuildAudience.Alliance && f.GuildAllianceId == null)
            .ToListAsync();
        var snowflakes = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId && s.Audience == GuildAudience.Alliance && s.GuildAllianceId == null)
            .ToListAsync();
        var texts = await db.GuildFeatureSettingTexts
            .Where(s => s.GuildId == guildId && s.Audience == GuildAudience.Alliance && s.GuildAllianceId == null)
            .ToListAsync();

        foreach (var row in enabled)
            row.GuildAllianceId = guildAllianceId;
        foreach (var row in snowflakes)
            row.GuildAllianceId = guildAllianceId;
        foreach (var row in texts)
            row.GuildAllianceId = guildAllianceId;

        await db.SaveChangesAsync();
        return enabled.Count + snowflakes.Count + texts.Count;
    }

    // Adopts orphaned Alliance settings for every guild onto its primary alliance. Startup
    // self-heal companion to the migration (which backfills guilds that already had links) —
    // covers guilds seeded/created without a GuildAlliance row at migration time.
    public async Task AdoptAllOrphansAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var guildIdsWithOrphans = await db.GuildEnabledFeatures
            .Where(f => f.Audience == GuildAudience.Alliance && f.GuildAllianceId == null)
            .Select(f => f.GuildId)
            .Union(db.GuildFeatureSettingSnowflakes
                .Where(s => s.Audience == GuildAudience.Alliance && s.GuildAllianceId == null)
                .Select(s => s.GuildId))
            .Union(db.GuildFeatureSettingTexts
                .Where(s => s.Audience == GuildAudience.Alliance && s.GuildAllianceId == null)
                .Select(s => s.GuildId))
            .Distinct()
            .ToListAsync();

        foreach (var guildId in guildIdsWithOrphans)
        {
            var primaryId = await GetPrimaryIdAsync(guildId);
            if (primaryId is not null)
                await AdoptOrphansAsync(guildId, primaryId.Value);
        }
    }
}
