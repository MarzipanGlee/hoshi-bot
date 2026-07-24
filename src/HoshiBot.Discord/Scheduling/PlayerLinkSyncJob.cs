using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
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
    PlayerLinkService playerLinkService,
    ILogger<PlayerLinkSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var guildIds = await featureService.GetEnabledGuildIdsAsync(GuildFeature.PlayerLink, cancellationToken);

        foreach (var guildId in guildIds)
        {
            if (!await featureService.IsEnabledAsync(guildId, GuildFeature.PlayerLink))
                continue;

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
        var linked = 0;
        var queued = 0;
        await foreach (var member in gatewayClient.Rest.GetGuildUsersAsync(guildId).WithCancellation(cancellationToken))
        {
            if (member.IsBot)
                continue;

            var outcome = await playerLinkService.ProcessMemberAsync(guildId, member.Id, CommanderName.Of(member));
            if (outcome == PlayerLinkOutcome.Linked)
                linked++;
            else if (outcome == PlayerLinkOutcome.Queued)
                queued++;
        }

        logger.LogInformation("PlayerLink guild {Guild}: linked {Linked}, queued {Queued} for admin review.", guildId, linked, queued);
    }
}
