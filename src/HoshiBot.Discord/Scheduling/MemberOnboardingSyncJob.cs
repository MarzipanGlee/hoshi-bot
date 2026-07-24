using HoshiBot.Data;
using HoshiBot.Discord.MemberOnboarding;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Drives the opt-in, guild-wide MemberOnboarding DM outreach: for each guild whose campaign is active,
// DMs members with an Unresolved PlayerLinkReview row (the ones the PlayerLink matcher couldn't
// auto-assign), paced by a per-day and per-run cap to stay clear of Discord's DM rate/anti-spam
// limits. Each send flips the row to DmSent (or Undeliverable); the button/modal handlers flip it to
// Resolved. When the feature is off, no DMs go out and unresolved members are handled purely via the
// admin assignment page.
//
// DisallowConcurrentExecution: the immediate first run at scheduler start plus a scheduled tick could
// otherwise both pick the same Unresolved row and double-DM it.
[DisallowConcurrentExecution]
public class MemberOnboardingSyncJob(
    HoshiBotDbContext db,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    MemberOnboardingService onboarding,
    ILogger<MemberOnboardingSyncJob> logger) : IJob
{
    private const int DefaultMaxPerDay = 10;
    private const int MaxPerRun = 5;

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        var guildIds = await featureService.GetEnabledGuildIdsAsync(GuildFeature.MemberOnboarding, cancellationToken);

        var sentThisRun = 0;
        foreach (var guildId in guildIds)
        {
            if (sentThisRun >= MaxPerRun)
                break;

            try
            {
                sentThisRun += await ProcessGuildAsync(guildId, MaxPerRun - sentThisRun, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MemberOnboarding outreach failed for guild {GuildId}", guildId);
            }
        }
    }

    private async Task<int> ProcessGuildAsync(ulong guildId, int runBudget, CancellationToken cancellationToken)
    {
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.MemberOnboarding))
            return 0;

        var campaignActive = await settingsService.GetTextAsync(guildId, GuildFeature.MemberOnboarding, GuildAudience.Community, null, MemberOnboardingSettingKeys.CampaignActive);
        if (!string.Equals(campaignActive, "true", StringComparison.OrdinalIgnoreCase))
            return 0;

        var maxPerDay = int.TryParse(
            await settingsService.GetTextAsync(guildId, GuildFeature.MemberOnboarding, GuildAudience.Community, null, MemberOnboardingSettingKeys.MaxInvitesPerDay),
            out var parsed) ? parsed : DefaultMaxPerDay;

        var dayAgo = DateTimeOffset.UtcNow.AddHours(-24);
        var sentToday = await db.PlayerLinkReviews
            .CountAsync(r => r.GuildId == guildId && r.Status == PlayerLinkReviewStatus.DmSent && r.UpdatedAt >= dayAgo, cancellationToken);

        var budget = Math.Min(maxPerDay - sentToday, runBudget);
        if (budget <= 0)
            return 0;

        var pending = await db.PlayerLinkReviews
            .Where(r => r.GuildId == guildId && r.Status == PlayerLinkReviewStatus.Unresolved)
            .OrderBy(r => r.CreatedAt)
            .Take(budget)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var review in pending)
        {
            await onboarding.SendOutreachAsync(review, cancellationToken);
            sent++;
        }

        logger.LogInformation("MemberOnboarding guild {Guild}: processed {Count} unresolved member(s) (budget {Budget}).",
            guildId, pending.Count, budget);
        return sent;
    }
}
