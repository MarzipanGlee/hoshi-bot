using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.MemberLore;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Sends member-lore interview opener DMs, paced. Per MemberLore-enabled alliance whose campaign is
// active: invite eligible members (hold the member role, not yet interviewed), most chat-active
// first, up to a per-day cap (MemberLoreSettingKeys.MaxInterviewsPerDay) and a small per-run cap so
// the sends spread out and stay clear of Discord's DM rate/anti-spam limits.
//
// DisallowConcurrentExecution: the immediate first run at scheduler start plus a scheduled tick
// could otherwise both invite the same member before either commits (unique (GuildId, DiscordUserId)
// collision + a double DM).
[DisallowConcurrentExecution]
public class MemberInterviewInviteJob(
    HoshiBotDbContext db,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureSettingsService settingsService,
    AiChatModelResolver modelResolver,
    MemberInterviewService interviews,
    ILogger<MemberInterviewInviteJob> logger) : IJob
{
    private const int DefaultMaxPerDay = 10;
    private const int MaxPerRun = 5;
    private const int ActivityWindowDays = 90;

    public Task Execute(IJobExecutionContext context) =>
        // recheckAudience null: MemberLore is per-alliance — the body gates on the guild's enabled
        // alliance links (GetEnabledAllianceIdsAsync), not a single audience.
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.MemberLore, null, logger,
            guildId => ProcessGuildAsync(guildId, context.CancellationToken), context.CancellationToken);

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var linkIds = await featureService.GetEnabledAllianceIdsAsync(guildId, GuildFeature.MemberLore);
        if (linkIds.Count == 0)
            return;

        // The interview reuses the guild's AI-chat model — don't DM anyone into a dead conversation
        // if it isn't usable (Gemini selected but no API key configured).
        var model = await modelResolver.ResolveAsync(guildId);
        if (model.Provider.Kind == AiProvider.Gemini && string.IsNullOrWhiteSpace(model.ApiKey))
        {
            logger.LogInformation("MemberLore campaign for guild {GuildId} skipped: AI chat (Gemini) has no API key configured.", guildId);
            return;
        }

        var sentThisRun = 0;
        foreach (var linkId in linkIds)
        {
            if (sentThisRun >= MaxPerRun)
                break;

            var campaignActive = await settingsService.GetTextAsync(guildId, GuildFeature.MemberLore, GuildAudience.Alliance, linkId, MemberLoreSettingKeys.CampaignActive);
            if (!string.Equals(campaignActive, "true", StringComparison.OrdinalIgnoreCase))
                continue;

            var link = await db.GuildAlliances.FirstOrDefaultAsync(ga => ga.Id == linkId, cancellationToken);
            if (link is null)
                continue;

            var memberRole = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.MemberLore, GuildAudience.Alliance, linkId, MemberLoreSettingKeys.MemberRole)
                ?? link.MemberRoleId;
            if (memberRole is not { } memberRoleId)
            {
                logger.LogInformation("MemberLore campaign active for guild {GuildId} alliance {LinkId} but no member role set; skipping.", guildId, linkId);
                continue;
            }

            var maxPerDay = int.TryParse(
                await settingsService.GetTextAsync(guildId, GuildFeature.MemberLore, GuildAudience.Alliance, linkId, MemberLoreSettingKeys.MaxInterviewsPerDay),
                out var parsed) ? parsed : DefaultMaxPerDay;

            var dayAgo = DateTimeOffset.UtcNow.AddHours(-24);
            var invitedToday = await db.MemberInterviews
                .CountAsync(i => i.GuildId == guildId && i.InvitedAt >= dayAgo && i.Status != MemberInterviewStatus.Undeliverable, cancellationToken);

            var budget = Math.Min(maxPerDay - invitedToday, MaxPerRun - sentThisRun);
            if (budget <= 0)
                continue;

            var alreadyInterviewed = (await db.MemberInterviews
                .Where(i => i.GuildId == guildId)
                .Select(i => i.DiscordUserId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var candidates = new List<(ulong Id, string Name)>();
            await foreach (var member in gatewayClient.Rest.GetGuildUsersAsync(guildId).WithCancellation(cancellationToken))
            {
                if (member.IsBot || alreadyInterviewed.Contains(member.Id) || !member.RoleIds.Contains(memberRoleId))
                    continue;
                candidates.Add((member.Id, CommanderName.Of(member)));
            }

            // budget already folds in the per-run cap (MaxPerRun − sentThisRun).
            var sentThisLink = 0;
            foreach (var userId in await RankByActivityAsync(guildId, candidates, cancellationToken))
            {
                if (sentThisLink >= budget)
                    break;
                if (await interviews.InviteAsync(guildId, linkId, userId, cancellationToken))
                {
                    sentThisLink++;
                    sentThisRun++;
                }
            }

            logger.LogInformation(
                "MemberLore guild {Guild} alliance {Link}: role {Role}, {Candidates} eligible member(s), invited {Sent} (budget {Budget}).",
                guildId, linkId, memberRoleId, candidates.Count, sentThisLink, budget);
        }
    }

    // Order candidates most-active first. Activity = count of recent indexed messages per author
    // name, matched to each member's CommanderName — approximate (the index has no author id), which
    // is fine for *ordering*: the daily cap invites everyone eventually, this just picks who's first.
    private async Task<List<ulong>> RankByActivityAsync(ulong guildId, List<(ulong Id, string Name)> candidates, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-ActivityWindowDays);
        var counts = await db.AiChatIndexedMessages
            .Where(m => m.GuildId == guildId && m.CreatedAt >= since && m.AuthorName != null)
            .GroupBy(m => m.AuthorName!)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count, cancellationToken);

        return candidates
            .OrderByDescending(c => counts.GetValueOrDefault(c.Name, 0))
            .Select(c => c.Id)
            .ToList();
    }
}
