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

    protected override StfcPlayerRank? TierOf(GuildPrimaryPlayer player) => player.Rank;

    protected override string RoleSettingKey(StfcPlayerRank tier) => RankRolesSettingKeys.RoleForRank(tier);

    protected override void LogSkippedMember(ulong userId, ulong guildId, HttpStatusCode statusCode) =>
        Logger.LogInformation(
            "Skipped rank role sync for user {UserId} in guild {GuildId}: {StatusCode}",
            userId, guildId, statusCode);
}
