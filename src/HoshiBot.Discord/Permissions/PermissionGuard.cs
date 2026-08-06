using System.Collections.Concurrent;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;

namespace HoshiBot.Discord.Permissions;

// What the bot is allowed to do in a guild, resolved once and reused — so a job asks "can I assign
// this role?" instead of asking Discord and being told no, member by member.
//
// Worth stating because it is the reason this is cheap: the answer comes from the gateway cache,
// which already holds every guild's roles and the bot's own member and is kept current by
// GUILD_ROLE_UPDATE / GUILD_MEMBER_UPDATE. A check costs NO API call. The 60-second cache is only to
// avoid recomputing the permission union and the role-position map once per member — at 956 members
// across nine jobs that is the difference that matters. The TTL is well under the 10-minute job
// cycle, so a permission an admin fixes is picked up on the next run with no invalidation plumbing.
public sealed record GuildRolePermissions(
    bool CanManageRoles,
    bool CanManageNicknames,
    int BotHighestRolePosition,
    ulong OwnerId,
    IReadOnlyDictionary<ulong, int> RolePositions)
{
    // Fail open on an unknown role: it may have been created since the snapshot, and refusing would
    // silently stop syncing a role that is probably fine.
    public bool CanAssign(ulong roleId) =>
        !RolePositions.TryGetValue(roleId, out var position)
        || RoleSyncEligibility.CanAssign(BotHighestRolePosition, position);

    public bool CanRename(ulong userId, IEnumerable<ulong> memberRoleIds)
    {
        var highest = memberRoleIds
            .Select(id => RolePositions.TryGetValue(id, out var position) ? position : 0)
            .DefaultIfEmpty(0)
            .Max();

        return RoleSyncEligibility.CanRename(BotHighestRolePosition, highest, userId == OwnerId);
    }
}

public sealed class PermissionGuard(GatewayClient gatewayClient, ILogger<PermissionGuard> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<ulong, (GuildRolePermissions Permissions, DateTimeOffset ResolvedAt)> _cache = new();

    // Null means "couldn't work it out" — the guild isn't in the gateway cache yet, or the bot's own
    // member isn't. EVERY caller must treat null as permission granted and carry on exactly as it
    // would have before. A wrong "no" here stops roles syncing silently, which is worse than the 403s
    // this exists to prevent; a wrong "yes" just costs the 403 we already pay today.
    public GuildRolePermissions? For(ulong guildId)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(guildId, out var cached) && now - cached.ResolvedAt < CacheTtl)
            return cached.Permissions;

        var resolved = Resolve(guildId);
        if (resolved is not null)
            _cache[guildId] = (resolved, now);

        return resolved;
    }

    private GuildRolePermissions? Resolve(ulong guildId)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild))
            return null;

        if (!guild.Users.TryGetValue(gatewayClient.Id, out var bot))
            return null;

        // Base permissions are the union of @everyone and the bot's own roles. @everyone is never in
        // RoleIds — its id is the guild id — so it has to be added explicitly, the same way
        // DiscordGuildDataService.GetBotRoleStatusAsync does it on the Web side.
        var permissions = guild.Roles.TryGetValue(guildId, out var everyone) ? everyone.Permissions : default;
        var highestPosition = 0;
        foreach (var roleId in bot.RoleIds)
        {
            if (!guild.Roles.TryGetValue(roleId, out var role))
                continue;

            permissions |= role.Permissions;
            highestPosition = Math.Max(highestPosition, role.RawPosition);
        }

        var domainPermissions = permissions.ToDomain();
        return new GuildRolePermissions(
            RoleSyncEligibility.CanManageRoles(domainPermissions),
            RoleSyncEligibility.CanManageNicknames(domainPermissions),
            highestPosition,
            guild.OwnerId,
            guild.Roles.ToDictionary(r => r.Key, r => r.Value.RawPosition));
    }

    // Logs the skip once per (guild, reason) per TTL rather than per member — the whole point is to
    // stop repeating ourselves, and that applies to the log as much as to the API calls.
    public void LogSkip(ulong guildId, string reason) =>
        logger.LogInformation("Skipping role sync in guild {GuildId}: {Reason}", guildId, reason);
}
