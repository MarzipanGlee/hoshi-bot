using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace HoshiBot.Discord.Scheduling;

// Keeps each guild's configured rank roles (RankRolesSettingKeys.{Admiral,Commodore,Premier,
// Operative,Agent}Role) in sync with each member's current STFC rank (StfcPlayer.Rank, set by
// a player-data import — see StfcPlayerImportService): the one role matching their rank is
// added, the other 4 are removed if present, so a promotion/demotion swaps the role instead
// of accumulating old ones. RankRoles is a single guild-wide feature (GuildAudience.Guild) —
// one set of 5 roles for the whole guild, not per-audience or per-alliance.
public class RankRoleSyncJob(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<RankRoleSyncJob> logger)
    : ExclusiveTierRoleSyncJob<StfcPlayerRank>(gatewayClient, featureService, settingsService, playerLinkService, logger)
{
    protected override GuildFeature Feature => GuildFeature.RankRoles;

    // No alliance, no rank — a rank is a position *within* an alliance. Guarded here as well as in
    // the import (which now clears the stored rank) because this corrects a member on the next
    // sweep instead of waiting for the next player import to touch their row. Null strips every
    // configured rank role they hold, which is exactly what should happen when they leave.
    protected override StfcPlayerRank? TierOf(GuildPrimaryPlayer player) =>
        player.AllianceId is null ? null : player.Rank;

    protected override string RoleSettingKey(StfcPlayerRank tier) => RankRolesSettingKeys.RoleForRank(tier);

    protected override string? NoTierRoleSettingKey => RankRolesSettingKeys.NoRankRole;

    // "No rank" is only a fact when we know they're in no alliance. A player who IS in one but has
    // no rank stored is missing data, not rankless — every player of a server that was seeded but
    // never imported looks like that, and badging them all "No Rank" is wrong.
    protected override bool NoTierApplies(GuildPrimaryPlayer player) => player.AllianceId is null;

    protected override void LogSkippedMember(ulong userId, ulong guildId, HttpStatusCode statusCode) =>
        Logger.LogInformation(
            "Skipped rank role sync for user {UserId} in guild {GuildId}: {StatusCode}",
            userId, guildId, statusCode);
}
