using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Gives every linked member a role named after their alliance's tag, so a coalition or server-wide
// Discord shows at a glance who is from where. A sibling of RankRoleSyncJob, but it cannot reuse
// ExclusiveTierRoleSyncJob: that one is generic over an *enum* of tiers whose roles are all
// configured up front, whereas an alliance tag is arbitrary text and the set of them is whatever
// the members happen to be in. So the roles are resolved (and optionally created) at runtime and
// remembered per alliance in the settings store — see AllianceTagRolesSettingKeys.RoleFor.
//
// Naming options (latinize/lowercase/prefix/suffix) only ever decide how a NEW role is NAMED;
// matching an existing one is always case-insensitive and accepts both the raw and the latinized
// spelling, so toggling an option never orphans the roles a guild already has.
public class AllianceTagRoleSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<AllianceTagRoleSyncJob> logger) : IJob
{
    // Discord's per-guild role cap. Worth respecting explicitly: a coalition Discord tracking many
    // alliances is exactly the case this feature is for, and a blind create loop would otherwise
    // spend the whole sweep collecting 400s.
    private const int DiscordMaxRolesPerGuild = 250;

    public Task Execute(IJobExecutionContext context) =>
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.AllianceTagRoles, GuildAudience.Guild, logger, SyncGuildAsync);

    private async Task SyncGuildAsync(ulong guildId)
    {
        var createMissing = IsOn(await GetAsync(guildId, AllianceTagRolesSettingKeys.CreateMissing), defaultOn: true);
        var latinize = IsOn(await GetAsync(guildId, AllianceTagRolesSettingKeys.Latinize), defaultOn: false);
        var lowercase = IsOn(await GetAsync(guildId, AllianceTagRolesSettingKeys.Lowercase), defaultOn: false);
        var prefix = await GetAsync(guildId, AllianceTagRolesSettingKeys.RolePrefix);
        var suffix = await GetAsync(guildId, AllianceTagRolesSettingKeys.RoleSuffix);
        var noAllianceRoleId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.AllianceTagRoles, GuildAudience.Guild, null, AllianceTagRolesSettingKeys.NoAllianceRole);
        var foreignRoleId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.AllianceTagRoles, GuildAudience.Guild, null, AllianceTagRolesSettingKeys.ForeignAllianceRole);

        var players = (await playerLinkService.GetGuildPrimaryPlayersAsync(guildId)).Values.ToList();
        if (players.Count == 0)
            return;

        // Home = the shared GuildServerScope definition, the same one Nickname Sync uses for its
        // "foreign player" tags and Server Tag Roles uses for its server list. Only these servers
        // get per-alliance roles: a Discord with visitors from all over would otherwise collect a
        // role per foreign alliance and walk straight into Discord's 250-role cap.
        var homeServerIds = (await GuildServerScope.ResolveAsync(db, guildId)).ServerIds;

        var roles = await gatewayClient.Rest.GetGuildRolesAsync(guildId);
        var bindings = await LoadBindingsAsync(guildId);

        // Drop bindings whose role was deleted in Discord, so the next step re-adopts or re-creates
        // one instead of syncing against an id that no longer exists.
        var liveRoleIds = roles.Select(r => r.Id).ToHashSet();
        foreach (var (allianceId, roleId) in bindings.Where(b => !liveRoleIds.Contains(b.Value)).ToList())
        {
            bindings.Remove(allianceId);
            await SetBindingAsync(guildId, allianceId, null);
        }

        // One role per HOME alliance actually represented among the members. Foreign ones share the
        // single ForeignAllianceRole instead, so they never reach the create loop below.
        var alliances = players
            .Where(p => p.AllianceId is not null && !string.IsNullOrWhiteSpace(p.AllianceTag) && homeServerIds.Contains(p.ServerId))
            .GroupBy(p => p.AllianceId!.Value)
            .ToDictionary(g => g.Key, g => g.First().AllianceTag!);

        var roleCount = roles.Count;
        foreach (var (allianceId, tag) in alliances)
        {
            if (bindings.ContainsKey(allianceId))
                continue;

            var candidates = AllianceTagRoleName.Candidates(tag, latinize, lowercase, prefix, suffix);
            if (roles.FirstOrDefault(r => candidates.Any(c => string.Equals(r.Name, c, StringComparison.OrdinalIgnoreCase))) is { } existing)
            {
                // Adopt a role the guild already had — this is what makes "create missing = off"
                // useful, and it stops a second role appearing next to a hand-made one.
                bindings[allianceId] = existing.Id;
                await SetBindingAsync(guildId, allianceId, existing.Id);
                continue;
            }

            if (!createMissing)
                continue;

            if (roleCount >= DiscordMaxRolesPerGuild)
            {
                logger.LogWarning(
                    "Guild {GuildId} is at Discord's {Cap}-role limit; not creating an alliance tag role for {Tag}",
                    guildId, DiscordMaxRolesPerGuild, tag);
                break;
            }

            try
            {
                var created = await gatewayClient.Rest.CreateGuildRoleAsync(guildId, new RoleProperties { Name = candidates[0] });
                roleCount++;
                bindings[allianceId] = created.Id;
                await SetBindingAsync(guildId, allianceId, created.Id);
            }
            catch (RestException ex)
            {
                // Most likely no Manage Roles permission. Log once per alliance per run and carry on
                // — the members whose role already exists should still get synced.
                logger.LogWarning(ex,
                    "Failed to create alliance tag role {Name} in guild {GuildId}", candidates[0], guildId);
            }
        }

        // Everything this feature owns in this guild. A member keeps exactly one of these (or none),
        // so a member who switches alliance loses the old one — which is why the bindings are
        // persisted rather than recomputed from the tags currently in use.
        var managedRoleIds = bindings.Values.ToHashSet();
        if (noAllianceRoleId is { } noneRole)
            managedRoleIds.Add(noneRole);
        if (foreignRoleId is { } foreignRole)
            managedRoleIds.Add(foreignRole);
        if (managedRoleIds.Count == 0)
            return;

        var roster = await GuildRoster.FetchAsync(gatewayClient, guildId);
        foreach (var player in players)
        {
            if (!roster.TryGetValue(player.DiscordUserId, out var guildUser))
                continue;

            // Three mutually exclusive states, in this order: no alliance at all beats "foreign",
            // because "Foreign Alliance" on someone who has no alliance would simply be wrong.
            var targetRoleId = player.AllianceId is not { } allianceId
                ? noAllianceRoleId
                : homeServerIds.Contains(player.ServerId)
                    ? bindings.GetValueOrDefault(allianceId)
                    : foreignRoleId;

            await SyncMemberAsync(guildId, guildUser, managedRoleIds, targetRoleId);
        }
    }

    // Same shape as ExclusiveTierRoleSyncJob.SyncMemberAsync: only ever touches role ids this
    // feature owns, and only issues a REST call when the member's state actually differs.
    private async Task SyncMemberAsync(ulong guildId, GuildUser guildUser, IEnumerable<ulong> managedRoleIds, ulong? targetRoleId)
    {
        try
        {
            foreach (var roleId in managedRoleIds)
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
            logger.LogInformation(
                "Skipped alliance tag role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                guildUser.Id, guildId, ex.StatusCode);
        }
    }

    // The alliance→role bindings, read straight from the settings table: there's no "get every key
    // with this prefix" on GuildFeatureSettingsService, and this is the only caller that wants one.
    private async Task<Dictionary<int, ulong>> LoadBindingsAsync(ulong guildId)
    {
        var rows = await db.GuildFeatureSettingSnowflakes
            .Where(s => s.GuildId == guildId
                && s.Feature == GuildFeature.AllianceTagRoles
                && s.Audience == GuildAudience.Guild
                && s.GuildAllianceId == null
                && s.Key.StartsWith(AllianceTagRolesSettingKeys.RolePrefixKey))
            .Select(s => new { s.Key, s.Value })
            .ToListAsync();

        var bindings = new Dictionary<int, ulong>();
        foreach (var row in rows)
        {
            if (int.TryParse(row.Key[AllianceTagRolesSettingKeys.RolePrefixKey.Length..], out var allianceId))
                bindings[allianceId] = row.Value;
        }

        return bindings;
    }

    private Task SetBindingAsync(ulong guildId, int stfcAllianceId, ulong? roleId) =>
        settingsService.SetSnowflakeAsync(guildId, GuildFeature.AllianceTagRoles, GuildAudience.Guild, null,
            AllianceTagRolesSettingKeys.RoleFor(stfcAllianceId), roleId);

    private Task<string?> GetAsync(ulong guildId, string key) =>
        settingsService.GetTextAsync(guildId, GuildFeature.AllianceTagRoles, GuildAudience.Guild, null, key);

    private static bool IsOn(string? stored, bool defaultOn) => defaultOn
        ? !string.Equals(stored, "false", StringComparison.OrdinalIgnoreCase)
        : string.Equals(stored, "true", StringComparison.OrdinalIgnoreCase);
}
