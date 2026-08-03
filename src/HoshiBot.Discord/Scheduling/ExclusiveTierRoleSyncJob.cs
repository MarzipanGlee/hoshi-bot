using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Shared engine for the "exactly one tier role per member" sync jobs (RankRoleSyncJob /
// OpsLevelRoleSyncJob / ServerTagRoleSyncJob): the one configured role matching a member's current
// tier is added, the other tiers' roles are removed if present, so moving between tiers swaps the
// role instead of accumulating old ones. All are single guild-wide features (GuildAudience.Guild) —
// one role set for the whole guild, not per-audience or per-alliance. Concrete jobs supply the
// feature, the tier list, the member → tier mapping and the tier → role-setting-key mapping; they
// must keep their exact class name and namespace — Quartz's persistent store (qrtz_job_details)
// records each job by the concrete type's fully-qualified name.
//
// TTier is any value type, not just an enum: Server Tag Roles' tiers are STFC server ids, whose set
// is per-guild configuration rather than something known at compile time. That's why the tier list
// is asked for (TiersAsync) instead of being read off the type.
public abstract class ExclusiveTierRoleSyncJob<TTier>(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger logger) : IJob
    where TTier : struct
{
    protected abstract GuildFeature Feature { get; }

    // Every tier this feature manages for this guild, i.e. the ones a role can be configured for.
    // Enum-backed features return all their enum values and ignore the guild id.
    protected abstract ValueTask<IReadOnlyList<TTier>> TiersAsync(ulong guildId);

    // The member's current tier, from whichever player represents them in this guild — null when
    // no tier applies (no import data yet), which strips every configured tier role they hold.
    protected abstract TTier? TierOf(GuildPrimaryPlayer player);

    protected abstract string RoleSettingKey(TTier tier);

    // Optional role for members who have no tier at all — e.g. a player with no alliance has no
    // rank. Null (the default) means the feature has no such role, and a tierless member simply
    // ends up holding none of them. Managed exactly like a tier role: added when it applies,
    // removed when it stops applying.
    protected virtual string? NoTierRoleSettingKey => null;

    // Whether a null TierOf means "this member genuinely has no tier" rather than "we have no data
    // for them". The two are indistinguishable in TierOf's return value but must not be treated
    // alike: acting on missing data badges every player of a server we've only seeded (and never
    // imported) as having no rank. When this returns false the member is skipped entirely — no role
    // added, none taken away — so whatever they hold survives until real data arrives.
    protected virtual bool NoTierApplies(GuildPrimaryPlayer player) => true;

    // Whether a tier that isn't in TiersAsync at all should be treated as "no tier" (getting the
    // no-tier role) rather than simply matching nothing. Distinct from a tier that IS in the list
    // but has no role configured yet — that one must always match nothing.
    //
    // Only meaningful when the tier set is per-guild: a server the guild doesn't count as its own
    // IS the foreign case, and its members belong in the foreign-server role. For an enum-backed
    // feature every value is always in the list, so this never fires.
    protected virtual bool UnknownTierIsNoTier => false;

    // Concrete jobs own the skip-log line so each keeps its original wording verbatim.
    protected abstract void LogSkippedMember(ulong userId, ulong guildId, HttpStatusCode statusCode);

    // Exposed for LogSkippedMember implementations — a concrete class capturing its own ctor logger
    // besides passing it to base would trip CS9107 (double capture).
    protected ILogger Logger => logger;

    public Task Execute(IJobExecutionContext context) =>
        // Guild-wide, guild-scoped (null): only act when enabled for the Guild audience, ignoring
        // any orphaned rows left under other audiences by the audience refactor.
        this.ForEachEnabledGuildAsync(featureService, Feature, GuildAudience.Guild, logger, SyncGuildAsync);

    private async Task SyncGuildAsync(ulong guildId)
    {
        // The tier comes from whichever player represents the member in *this* guild.
        var players = (await playerLinkService.GetGuildPrimaryPlayersAsync(guildId)).Values.ToList();

        var roster = await GuildRoster.FetchAsync(gatewayClient, guildId);
        await SyncAudienceAsync(guildId, GuildAudience.Guild, null, players, roster);
    }

    private async Task SyncAudienceAsync(ulong guildId, GuildAudience audience, int? guildAllianceId, IReadOnlyList<GuildPrimaryPlayer> players, IReadOnlyDictionary<ulong, GuildUser> roster)
    {
        var tiers = await TiersAsync(guildId);
        var tierSet = tiers.ToHashSet();

        var roleIdsByTier = new Dictionary<TTier, ulong>();
        foreach (var tier in tiers)
        {
            var roleId = await settingsService.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, RoleSettingKey(tier));
            if (roleId is { } id)
                roleIdsByTier[tier] = id;
        }

        var noTierRoleId = NoTierRoleSettingKey is { } noTierKey
            ? await settingsService.GetSnowflakeAsync(guildId, Feature, audience, guildAllianceId, noTierKey)
            : null;

        if (roleIdsByTier.Count == 0 && noTierRoleId is null)
            return;

        // Everything this feature owns: the configured tier roles plus the no-tier role, so a member
        // moving between the two has the old one taken off.
        var managedRoleIds = roleIdsByTier.Values.ToList();
        if (noTierRoleId is { } noTier)
            managedRoleIds.Add(noTier);

        foreach (var player in players)
        {
            if (!roster.TryGetValue(player.DiscordUserId, out var guildUser))
                continue;
            var tier = TierOf(player);
            if (tier is null && !NoTierApplies(player))
                continue;

            // A tier with no configured role yields 0 here (not null) — deliberately: the member
            // then matches no role, so stale tier roles still get removed. A tier that isn't in the
            // list at all is a different thing, and UnknownTierIsNoTier decides which.
            var targetRoleId = tier is { } t
                ? tierSet.Contains(t) || !UnknownTierIsNoTier ? roleIdsByTier.GetValueOrDefault(t) : noTierRoleId
                : noTierRoleId;
            await SyncMemberAsync(guildId, guildUser, managedRoleIds, targetRoleId);
        }
    }

    private async Task SyncMemberAsync(ulong guildId, GuildUser guildUser, IEnumerable<ulong> allTierRoleIds, ulong? targetRoleId)
    {
        try
        {
            foreach (var roleId in allTierRoleIds)
            {
                var hasRole = guildUser.RoleIds.Contains(roleId);
                var shouldHaveRole = roleId == targetRoleId;

                if (shouldHaveRole && !hasRole)
                    await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, guildUser.Id, roleId);
                else if (!shouldHaveRole && hasRole)
                    await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, guildUser.Id, roleId);
            }
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            LogSkippedMember(guildUser.Id, guildId, ex.StatusCode);
        }
    }
}
