using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.ConditionalRoles;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Applies the guild's Conditional Roles rules: a member holds a rule's target role exactly while
// their roles satisfy that rule's condition tree, and loses it as soon as they don't.
//
// Not an ExclusiveTierRoleSyncJob — those grant one role out of a set, whereas conditional roles are
// independent of each other and a member can hold any number of them. The shape it does share is the
// managed set: this job only ever touches roles some enabled rule targets, so hand-assigned roles
// and other features' roles are never disturbed.
//
// Two behaviours worth knowing rather than being surprised by:
//   - Rules typically read roles another job maintains (alliance tag, server tag, rank). Those run
//     on their own 10-minute timers, so a change can take two sweeps to settle everywhere.
//   - A rule may read a role another rule grants. That converges, but two rules written to
//     contradict each other would flip a member back and forth once per sweep.
public class ConditionalRoleSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    ConditionalRoleService conditionalRoles,
    PlayerLinkService playerLinkService,
    ILogger<ConditionalRoleSyncJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.ConditionalRoles, GuildAudience.Guild, logger, SyncGuildAsync);

    private async Task SyncGuildAsync(ulong guildId)
    {
        var snapshot = await conditionalRoles.LoadAsync(guildId);
        if (snapshot.Rules.Count == 0)
            return;

        // Everything this feature owns here. A role stays in the set even if its rule currently
        // matches nobody — that is precisely how it gets taken off the members still holding it.
        var managedRoleIds = snapshot.Rules.Select(r => r.TargetRoleId).ToHashSet();

        // The player side of MemberFacts. "One of ours" is asked against the same GuildServerScope
        // the tag-role features use, so a rule means the same thing everywhere.
        var scope = await GuildServerScope.ResolveAsync(db, guildId);
        var players = await playerLinkService.GetGuildPrimaryPlayersAsync(guildId);

        var roster = await GuildRoster.FetchAsync(gatewayClient, guildId);
        var granted = 0;
        var removed = 0;

        foreach (var guildUser in roster.Values)
        {
            if (guildUser.IsBot)
                continue;

            // No linked player → Player stays null, and the player-fact leaves answer Unknown rather
            // than "no" (see ConditionEvaluator).
            var facts = new MemberFacts(
                guildUser.RoleIds.ToHashSet(),
                players.TryGetValue(guildUser.Id, out var player)
                    ? new PlayerFacts(
                        player.AllianceId is { } allianceId && scope.AllianceIds.Contains(allianceId),
                        scope.ServerIds.Contains(player.ServerId))
                    : null);

            // Per role: a definite match from any rule grants it, and only a definite no from every
            // rule removes it. An Unknown anywhere leaves the role exactly as it is — a member whose
            // player data hasn't imported must not lose roles over a question we couldn't answer.
            var target = managedRoleIds.ToDictionary(id => id, _ => ConditionOutcome.NoMatch);
            foreach (var rule in snapshot.Rules)
            {
                if (rule.Root is null)
                    continue;

                var outcome = ConditionEvaluator.Evaluate(rule.Root, facts, snapshot.Conditions);
                var current = target[rule.TargetRoleId];
                if (outcome == ConditionOutcome.Match || current == ConditionOutcome.NoMatch)
                    target[rule.TargetRoleId] = outcome == ConditionOutcome.Match ? ConditionOutcome.Match : outcome;
            }

            var (added, took) = await SyncMemberAsync(guildId, guildUser, target);
            granted += added;
            removed += took;
        }

        if (granted > 0 || removed > 0)
        {
            logger.LogInformation(
                "Conditional roles guild {GuildId}: {Rules} rule(s) over {Roles} role(s), granted {Granted}, removed {Removed}.",
                guildId, snapshot.Rules.Count, managedRoleIds.Count, granted, removed);
        }
    }

    // Same add/remove-within-the-managed-set shape as the other role syncs; the difference is that
    // several roles can apply at once, so this walks the whole map rather than matching one winner.
    private async Task<(int Granted, int Removed)> SyncMemberAsync(
        ulong guildId, GuildUser guildUser, Dictionary<ulong, ConditionOutcome> targetByRole)
    {
        var granted = 0;
        var removed = 0;

        try
        {
            foreach (var (roleId, outcome) in targetByRole)
            {
                // Unknown means "we couldn't tell" — not a reason to touch anything.
                if (outcome == ConditionOutcome.Unknown)
                    continue;

                var shouldHave = outcome == ConditionOutcome.Match;
                var hasRole = guildUser.RoleIds.Contains(roleId);
                if (shouldHave && !hasRole)
                {
                    await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, guildUser.Id, roleId);
                    granted++;
                }
                else if (!shouldHave && hasRole)
                {
                    await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, guildUser.Id, roleId);
                    removed++;
                }
            }
        }
        catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            // Forbidden here almost always means the target role sits above the bot in the guild's
            // role hierarchy — logged explicitly because that failure mode is otherwise invisible
            // and has bitten this community before on a legacy bot.
            logger.LogInformation(
                "Skipped conditional role sync for user {UserId} in guild {GuildId}: {StatusCode} (is the target role above the bot's own role?)",
                guildUser.Id, guildId, ex.StatusCode);
        }

        return (granted, removed);
    }
}
