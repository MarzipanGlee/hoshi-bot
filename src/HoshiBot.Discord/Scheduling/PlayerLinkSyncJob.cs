using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Backfills automated player↔member assignment for the PlayerLink feature: for each enabled
// alliance, walks its member-role holders and runs the matcher (PlayerLinkService.ProcessMemberAsync)
// — a confident single in-alliance nickname match links silently, everything else becomes an
// Unresolved PlayerLinkReview row for the Web admin table. Writes only DB rows (no DMs, no pacing);
// the on-join MemberJoinHandler does the same per new member in real time.
//
// DisallowConcurrentExecution: the immediate first run at scheduler start plus a scheduled tick could
// otherwise both process the same member before either commits and collide on the review's unique
// (GuildId, DiscordUserId) index (or double-insert a UserPlayer link).
[DisallowConcurrentExecution]
public class PlayerLinkSyncJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    PlayerLinkService playerLinkService,
    ILogger<PlayerLinkSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var guildIds = await db.GuildEnabledFeatures
            .Where(f => f.Feature == GuildFeature.PlayerLink)
            .Select(f => f.GuildId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var guildId in guildIds)
        {
            try
            {
                await ProcessGuildAsync(guildId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PlayerLink backfill failed for guild {GuildId}", guildId);
            }
        }
    }

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var linkIds = await featureService.GetEnabledAllianceIdsAsync(guildId, GuildFeature.PlayerLink);
        if (linkIds.Count == 0)
            return;

        foreach (var linkId in linkIds)
        {
            var link = await db.GuildAlliances.FirstOrDefaultAsync(ga => ga.Id == linkId, cancellationToken);
            if (link is null)
                continue;

            var memberRole = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.PlayerLink, GuildAudience.Alliance, linkId, PlayerLinkSettingKeys.MemberRole)
                ?? link.MemberRoleId;
            if (memberRole is not { } memberRoleId)
            {
                logger.LogInformation("PlayerLink enabled for guild {GuildId} alliance {LinkId} but no member role set; skipping.", guildId, linkId);
                continue;
            }

            var linked = 0;
            var queued = 0;
            await foreach (var member in gatewayClient.Rest.GetGuildUsersAsync(guildId).WithCancellation(cancellationToken))
            {
                if (member.IsBot || !member.RoleIds.Contains(memberRoleId))
                    continue;

                var outcome = await playerLinkService.ProcessMemberAsync(guildId, linkId, member.Id, CommanderName.Of(member));
                if (outcome == PlayerLinkOutcome.Linked)
                    linked++;
                else if (outcome == PlayerLinkOutcome.Queued)
                    queued++;
            }

            logger.LogInformation(
                "PlayerLink guild {Guild} alliance {Link}: role {Role}, linked {Linked}, queued {Queued} for admin review.",
                guildId, linkId, memberRoleId, linked, queued);
        }
    }
}
