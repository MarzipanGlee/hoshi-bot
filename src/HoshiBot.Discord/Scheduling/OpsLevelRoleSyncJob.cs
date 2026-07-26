using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace HoshiBot.Discord.Scheduling;

// Keeps each guild's configured Ops Level roles (OpsLevelRolesSettingKeys.{G1..G7}Role) in
// sync with each member's current STFC Ops Level (StfcPlayer.OpsLevel, set by a player-data
// import — see StfcPlayerImportService), bucketed into a G1-G7 tier via
// StfcOpsGroupExtensions.FromLevel: the one role matching their tier is added, the other 6
// are removed if present, so leveling up/down swaps the role instead of accumulating old
// ones. OpsLevelRoles is a single guild-wide feature (GuildAudience.Guild) — one set of 7
// roles for the whole guild, not per-audience or per-alliance.
public class OpsLevelRoleSyncJob(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<OpsLevelRoleSyncJob> logger)
    : ExclusiveTierRoleSyncJob<StfcOpsGroup>(gatewayClient, featureService, settingsService, playerLinkService, logger)
{
    protected override GuildFeature Feature => GuildFeature.OpsLevelRoles;

    protected override StfcOpsGroup? TierOf(GuildPrimaryPlayer player) => StfcOpsGroupExtensions.FromLevel(player.OpsLevel);

    protected override string RoleSettingKey(StfcOpsGroup tier) => OpsLevelRolesSettingKeys.RoleForGroup(tier);

    protected override void LogSkippedMember(ulong userId, ulong guildId, HttpStatusCode statusCode) =>
        Logger.LogInformation(
            "Skipped Ops level role sync for user {UserId} in guild {GuildId}: {StatusCode}",
            userId, guildId, statusCode);
}
