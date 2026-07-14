using HoshiBot.Domain.Entities;

namespace HoshiBot.Data;

// Enforces the one invariant behind per-alliance feature scoping: GuildAllianceId is set
// exactly for the Alliance audience and null for every other audience. Guards the service API
// (GuildFeatureService / GuildFeatureSettingsService) rather than the database — a DB check
// constraint would break SeedGuildSettingsIfEmptyAsync, which writes Alliance-audience rows
// with a null scope that a later self-heal (GuildAllianceService.AdoptOrphansAsync) assigns
// once the guild's alliance is linked.
internal static class FeatureScopeGuard
{
    public static void Validate(GuildAudience audience, int? guildAllianceId)
    {
        var isAlliance = audience == GuildAudience.Alliance;
        if (isAlliance != guildAllianceId.HasValue)
        {
            throw new ArgumentException(
                $"Audience {audience} requires guildAllianceId {(isAlliance ? "to be set" : "to be null")}.",
                nameof(guildAllianceId));
        }
    }
}
