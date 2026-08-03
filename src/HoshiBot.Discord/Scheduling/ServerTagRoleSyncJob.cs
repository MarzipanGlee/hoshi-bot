using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;

namespace HoshiBot.Discord.Scheduling;

// Keeps each guild's configured server roles (ServerTagRolesSettingKeys.RoleFor) in sync with the
// server each member actually plays on: the one role matching their server is added, the others are
// removed, so a member who moves servers swaps the role. Members playing anywhere outside the
// guild's own servers share the single ForeignServerRole.
//
// Same engine as the rank/ops-level roles, with STFC server ids as tiers instead of an enum — the
// tier set is per-guild configuration (GuildServerScope), which is what TiersAsync exists for.
public class ServerTagRoleSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<ServerTagRoleSyncJob> logger)
    : ExclusiveTierRoleSyncJob<int>(gatewayClient, featureService, settingsService, playerLinkService, logger)
{
    protected override GuildFeature Feature => GuildFeature.ServerTagRoles;

    // The guild's own servers — its linked alliances' servers plus any it tracks directly. The same
    // list the editor offers a role picker for.
    protected override async ValueTask<IReadOnlyList<int>> TiersAsync(ulong guildId) =>
        (await GuildServerScope.ResolveAsync(db, guildId)).ServerIds.ToList();

    // Every player sits on exactly one server, so this never returns null and the no-tier path is
    // reached only via UnknownTierIsNoTier below.
    protected override int? TierOf(GuildPrimaryPlayer player) => player.ServerId;

    protected override string RoleSettingKey(int tier) => ServerTagRolesSettingKeys.RoleFor(tier);

    protected override string? NoTierRoleSettingKey => ServerTagRolesSettingKeys.ForeignServerRole;

    // A server that isn't one of the guild's own IS the foreign case — that's the whole point of the
    // foreign-server role. A server that IS one of them but has no role picked yet still matches
    // nothing (the base's default), so an unconfigured server never badges anyone as foreign.
    protected override bool UnknownTierIsNoTier => true;

    protected override void LogSkippedMember(ulong userId, ulong guildId, HttpStatusCode statusCode) =>
        Logger.LogInformation(
            "Skipped server tag role sync for user {UserId} in guild {GuildId}: {StatusCode}",
            userId, guildId, statusCode);
}
