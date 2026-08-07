using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Discord.AnnouncementForwarder;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Catches up on announcements the live MESSAGE_CREATE path missed — most commonly, one posted while
// the bot was down for a redeploy. Re-scans each source channel's recent messages and re-runs
// AnnouncementForwarderService.MaybeForwardAsync on them; its own ForwardedAnnouncements lookup makes
// this idempotent (an already-forwarded message is a cheap no-op), so this job is the entire
// "catch-up" mechanism — no separate startup hook needed.
//
// Time-bounded, not count-bounded: these source channels post rarely (roughly once every few days), so
// a fixed message count would span months of history — resurrecting stale, no-longer-relevant
// announcements on first enable or after a long outage. Only messages newer than the guild's configured
// catch-up window are ever forwarded here.
//
// DisallowConcurrentExecution: the immediate first run plus a scheduled tick could otherwise both
// forward the same missed message before either commits its tracking row.
[DisallowConcurrentExecution]
public class AnnouncementForwarderCatchUpJob(
    GuildFeatureService featureService,
    GuildFeatureChannelService channelService,
    GuildFeatureSettingsService settingsService,
    AiChatIndexService indexService,
    AnnouncementForwarderService forwarder,
    ILogger<AnnouncementForwarderCatchUpJob> logger) : IJob
{

    // Recent-messages page size per channel — generous relative to how rarely these channels post,
    // so a normal catch-up window is comfortably covered in one page without deep pagination.
    private const int MessagesPerChannel = 20;

    public Task Execute(IJobExecutionContext context) =>
        // recheckAudience null: the audience re-check stays at the top of ProcessGuildAsync (inside
        // the per-guild catch), exactly where it was before the runner extraction.
        this.ForEachEnabledGuildAsync(featureService, GuildFeature.AnnouncementForwarder, null, logger,
            guildId => ProcessGuildAsync(guildId, context.CancellationToken), context.CancellationToken);

    private async Task<int> WidestCatchUpWindowAsync(ulong guildId)
    {
        var widest = 0;
        foreach (var audience in GuildFeatureAudiences.EnumerateFlags(GuildFeatureAudiences.RelevantAudiences(GuildFeature.AnnouncementForwarder)))
        {
            var allianceIds = audience == GuildAudience.Alliance
                ? (await featureService.GetEnabledAllianceIdsAsync(guildId, GuildFeature.AnnouncementForwarder)).Select(id => (int?)id).ToList()
                : [null];

            foreach (var allianceId in allianceIds)
            {
                if (int.TryParse(await settingsService.GetTextAsync(guildId, GuildFeature.AnnouncementForwarder, audience, allianceId, AnnouncementForwarderSettingKeys.CatchUpWindowHours), out var hours))
                    widest = Math.Max(widest, hours);
            }
        }

        return widest > 0 ? widest : AnnouncementForwarderSettingKeys.DefaultCatchUpWindowHours;
    }

    private async Task ProcessGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.AnnouncementForwarder))
            return;

        var sourceChannels = await channelService.GetEnabledAudienceChannelsAsync(guildId, GuildFeature.AnnouncementForwarder);
        if (sourceChannels.Count == 0)
            return;

        // How far back to look is a property of the SOURCE sweep, which is shared across scopes —
        // so the widest configured window wins rather than an arbitrary scope's. MaybeForwardAsync
        // then decides per destination what still needs doing.
        var windowHours = await WidestCatchUpWindowAsync(guildId);
        var cutoff = DateTimeOffset.UtcNow.AddHours(-windowHours);

        // FetchRecentAsync returns Discord's natural newest-first order; gather every channel's
        // candidates and sort oldest-first before forwarding, so a multi-message catch-up run posts
        // them in the same order they were originally announced instead of backwards.
        var candidates = new List<RestMessage>();
        foreach (var channelId in sourceChannels)
        {
            var recent = await indexService.FetchRecentAsync(channelId, MessagesPerChannel, cancellationToken) ?? [];
            candidates.AddRange(recent.Where(m => m.CreatedAt >= cutoff));
        }

        foreach (var message in candidates.OrderBy(m => m.CreatedAt))
            await forwarder.MaybeForwardAsync(guildId, message, cancellationToken);

        if (candidates.Count > 0)
            logger.LogInformation("Announcement forwarder catch-up for guild {Guild}: checked {Count} message(s) within the {Hours}h window.", guildId, candidates.Count, windowHours);
    }
}
