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
    MemberRoles memberRoles,
    AiChatModelResolver modelResolver,
    MemberInterviewService interviews,
    ILogger<MemberInterviewInviteJob> logger) : IJob
{
    private const int DefaultMaxPerDay = 10;
    private const int MaxPerRun = 5;
    private const int ActivityWindowDays = 90;

    public Task Execute(IJobExecutionContext context) =>
        // recheckAudience null: MemberLore runs per SCOPE — the body walks every enabled
        // (audience, alliance) pair rather than one audience or one alliance list.
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.MemberLore, null, logger,
            guildId => ProcessGuildAsync(guildId, context.CancellationToken), context.CancellationToken);

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        // Every scope the feature is on for. It used to be the alliance links alone, which is all
        // there was when Member Lore was an alliance-only feature.
        var scopes = (await db.GuildEnabledFeatures
            .Where(f => f.GuildId == guildId && f.Feature == GuildFeature.MemberLore)
            .Select(f => new { f.Audience, f.GuildAllianceId })
            .ToListAsync(cancellationToken))
            .Select(f => (f.Audience, f.GuildAllianceId))
            .ToList();
        if (scopes.Count == 0)
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
        foreach (var (audience, linkId) in scopes)
        {
            if (sentThisRun >= MaxPerRun)
                break;

            var campaignActive = await settingsService.GetTextAsync(guildId, GuildFeature.MemberLore, audience, linkId, MemberLoreSettingKeys.CampaignActive);
            if (!string.Equals(campaignActive, "true", StringComparison.OrdinalIgnoreCase))
                continue;

            // The feature's own override, else the scope's member role — which for an alliance is
            // GuildAlliance.MemberRoleId and for the other audiences GuildAudienceSettings'.
            var memberRole = await settingsService.GetSnowflakeAsync(guildId, GuildFeature.MemberLore, audience, linkId, MemberLoreSettingKeys.MemberRole)
                ?? await memberRoles.ForScopeAsync(guildId, audience, linkId);
            if (memberRole is not { } memberRoleId)
            {
                logger.LogInformation("MemberLore campaign active for guild {GuildId} scope {Audience}/{LinkId} but no member role set; skipping.", guildId, audience, linkId);
                continue;
            }

            var maxPerDay = int.TryParse(
                await settingsService.GetTextAsync(guildId, GuildFeature.MemberLore, audience, linkId, MemberLoreSettingKeys.MaxInterviewsPerDay),
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
                if (await interviews.InviteAsync(guildId, audience, linkId, userId, cancellationToken))
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
