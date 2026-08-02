using System.Net;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Backfills player↔member assignment for the (guild-wide) PlayerLink feature: for each enabled guild,
// walks every non-bot member and runs the matcher (PlayerLinkService.ProcessMemberAsync) — a
// confident nickname match links silently, everything else becomes an Unresolved PlayerLinkReview for
// the admin assignment page / onboarding. Writes only DB rows (no DMs); the on-join/update
// MemberOnboardingHandler does the same per member in real time.
//
// DisallowConcurrentExecution: the immediate first run at scheduler start plus a scheduled tick could
// otherwise both process the same member before either commits and collide on the review's unique
// (GuildId, DiscordUserId) index (or double-insert a UserPlayer link).
[DisallowConcurrentExecution]
public class PlayerLinkSyncJob(
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<PlayerLinkSyncJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context) =>
        // recheckAudience null: this job re-checks with the guild-wide IsEnabledAsync overload
        // (enabled under any relevant audience) below, not the audience-explicit one.
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.PlayerLink, null, logger,
            guildId => ProcessGuildAsync(guildId, context.CancellationToken), context.CancellationToken);

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.PlayerLink))
            return;

        var linked = 0;
        var queued = 0;
        var members = new List<GuildUser>();
        await foreach (var member in gatewayClient.Rest.GetGuildUsersAsync(guildId).WithCancellation(cancellationToken))
        {
            if (member.IsBot)
                continue;

            members.Add(member);
            var outcome = await playerLinkService.ProcessMemberAsync(guildId, member.Id, CommanderName.Of(member));
            if (outcome == PlayerLinkOutcome.Linked)
                linked++;
            else if (outcome == PlayerLinkOutcome.Queued)
                queued++;
        }

        logger.LogInformation("PlayerLink guild {Guild}: linked {Linked}, queued {Queued} for admin review.", guildId, linked, queued);

        await SyncUnlinkedRoleAsync(guildId, members);
    }

    // The optional "not linked yet" role. Read AFTER the matcher loop above, so a member it just
    // linked doesn't get the role for one cycle. This is the one role sync that walks the whole
    // roster rather than the members with player data — it's defined by the absence of that data.
    private async Task SyncUnlinkedRoleAsync(ulong guildId, IReadOnlyList<GuildUser> members)
    {
        var roleId = await settingsService.GetSnowflakeAsync(
            guildId, GuildFeature.PlayerLink, GuildAudience.Guild, null, PlayerLinkSettingKeys.UnlinkedRole);
        if (roleId is not { } unlinkedRoleId)
            return;

        // "Linked" means the same thing here as it does to every other sync: a player represents
        // this member in this guild (their own pick, else their oldest link).
        var linkedUserIds = (await playerLinkService.GetGuildPrimaryPlayersAsync(guildId)).Keys.ToHashSet();

        foreach (var member in members)
        {
            var shouldHaveRole = !linkedUserIds.Contains(member.Id);
            var hasRole = member.RoleIds.Contains(unlinkedRoleId);
            if (shouldHaveRole == hasRole)
                continue;

            try
            {
                if (shouldHaveRole)
                    await gatewayClient.Rest.AddGuildUserRoleAsync(guildId, member.Id, unlinkedRoleId);
                else
                    await gatewayClient.Rest.RemoveGuildUserRoleAsync(guildId, member.Id, unlinkedRoleId);
            }
            catch (RestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                logger.LogInformation(
                    "Skipped unlinked-player role sync for user {UserId} in guild {GuildId}: {StatusCode}",
                    member.Id, guildId, ex.StatusCode);
            }
        }
    }
}
