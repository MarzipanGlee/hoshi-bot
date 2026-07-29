using System.Globalization;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using HoshiBot.Data;
using HoshiBot.Discord.Notifications;
using HoshiBot.Discord.StfcNews;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using Quartz;

namespace HoshiBot.Discord.Scheduling;

// Polls Scopely's official WordPress blog RSS feed for new Alliance Tournament / Infinite
// Incursions posts and pings each opted-in guild's AdminChannelId — replaces the stale/never-
// live stfc.pro events JSON (StfcEventStatus is seed-data only; stfc.pro's robots.txt
// disallows automated /api/ access) with a real, live, permitted source. Detection is a
// case-insensitive substring match on the post TITLE only — <category> is NOT the primary
// signal; confirmed unreliable (a real Infinite Incursions post shipped tagged only the
// generic "News" category). Doesn't extract dates from the post body — the matching post's
// EventGroup drives StfcNewsService's admin date-entry/confirmation flow instead.
//
// DisallowConcurrentExecution: Quartz's default SimpleTrigger fires an immediate first run at
// scheduler start, and this job is slow enough (feed fetch + a per-guild member-count scan)
// that a second overlapping run confirmed, in practice, can start before the first commits —
// both then see a newly-detected post as "not yet known" and collide inserting the same
// StfcNewsPost.Link. Same class of bug as StfcClientReleaseNotifyJob's first-sight-insert race,
// same fix.
[DisallowConcurrentExecution]
public class StfcNewsNotifyJob(
    IHttpClientFactory httpClientFactory,
    HoshiBotDbContext db,
    NotificationDispatcher dispatcher,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    LanguageResolver languageResolver,
    ILogger<StfcNewsNotifyJob> logger) : IJob
{
    private const string FeedUrl = "https://startrekfleetcommand.com/feed/";

    // Title substring -> StfcEventStatus.EventGroup. Do not add <category>-based matching
    // here without re-confirming Infinite Incursions posts reliably carry a dedicated
    // category (they didn't, as of this writing).
    private static readonly (string Keyword, string EventGroup)[] TitleKeywordToEventGroup =
    [
        ("Alliance Tournament", "alliance_tournaments"),
        ("Infinite Incursions", "incursions"),
    ];

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var enabledGuildIds = await featureService.GetEnabledGuildIdsAsync(GuildFeature.StfcNews, ct);

        if (enabledGuildIds.Count == 0)
            return;

        List<(string Title, string Link, DateTimeOffset? PublishedAt, string EventGroup)> matches;
        try
        {
            matches = await FetchMatchingPostsAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException)
        {
            logger.LogWarning(ex, "Could not fetch/parse the STFC news RSS feed ({FeedUrl}).", FeedUrl);
            matches = [];
        }

        if (matches.Count > 0)
        {
            var matchedLinks = matches.Select(m => m.Link).ToList();
            var knownLinks = (await db.StfcNewsPosts
                    .Where(p => matchedLinks.Contains(p.Link))
                    .Select(p => p.Link)
                    .ToListAsync(ct))
                .ToHashSet();

            var newPosts = matches.Where(m => !knownLinks.Contains(m.Link)).ToList();
            if (newPosts.Count > 0)
            {
                var settings = await db.StfcNewsSettings.FindAsync([1], ct) ?? new StfcNewsSettings { Id = 1 };
                foreach (var post in newPosts)
                    await ProcessNewPostAsync(post, enabledGuildIds, settings.RequiredConfirmationPercentage, ct);
            }
        }

        // Covers a guild enabling StfcNews after a post was already detected — new-post
        // detection above only pings whichever guilds were enabled at that exact moment, so
        // without this a later-enabled guild would never see an unresolved post at all.
        // Independent of whether today's feed fetch above succeeded.
        await CatchUpNewlyEnabledGuildsAsync(enabledGuildIds, ct);
    }

    // Sends the current-state ping to any enabled guild that doesn't yet have a message for
    // an unresolved post. Deliberately does NOT recompute RequiredConfirmations — that's fixed
    // at first detection by design (see StfcNewsPost.RequiredConfirmations), so a guild joining
    // later doesn't shift the target out from under guilds already confirming.
    private async Task CatchUpNewlyEnabledGuildsAsync(List<ulong> enabledGuildIds, CancellationToken ct)
    {
        var unresolvedPosts = await db.StfcNewsPosts
            .Include(p => p.GuildMessages)
            .Where(p => p.ResolvedAt == null)
            .ToListAsync(ct);

        foreach (var post in unresolvedPosts)
        {
            var alreadyPingedGuildIds = post.GuildMessages.Select(m => m.GuildId).ToHashSet();
            var missingGuildIds = enabledGuildIds.Where(id => !alreadyPingedGuildIds.Contains(id)).ToList();
            if (missingGuildIds.Count == 0)
                continue;

            var confirmationCount = await db.StfcEventDateConfirmations.CountAsync(c => c.StfcNewsPostId == post.Id, ct);

            foreach (var guildId in missingGuildIds)
            {
                // The message lands in the guild-scoped AdminChannelId — that guild's language.
                var (embed, buttons) = StfcNewsMessageBuilder.Build(post, confirmationCount, await languageResolver.ForGuildAsync(guildId));
                var sent = await dispatcher.SendToAdminChannelAsync(guildId, embed: embed, buttons: buttons);
                if (sent is not { } message)
                    continue;

                db.StfcNewsPostGuildMessages.Add(new StfcNewsPostGuildMessage
                {
                    StfcNewsPostId = post.Id,
                    GuildId = guildId,
                    ChannelId = message.ChannelId,
                    MessageId = message.MessageId,
                    EligibleMemberCount = await ComputeEligibleMemberCountAsync(guildId, ct),
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // Two passes so RequiredConfirmations (a percentage of the pool combined across every
    // pinged guild) is known before any message is actually sent, avoiding a wasted
    // follow-up edit to add the number in afterward.
    private async Task ProcessNewPostAsync(
        (string Title, string Link, DateTimeOffset? PublishedAt, string EventGroup) post,
        List<ulong> enabledGuildIds, int requiredConfirmationPercentage, CancellationToken ct)
    {
        var eligibleCountsByGuild = new Dictionary<ulong, int>();
        foreach (var guildId in enabledGuildIds)
            eligibleCountsByGuild[guildId] = await ComputeEligibleMemberCountAsync(guildId, ct);

        var total = eligibleCountsByGuild.Values.Sum();
        var requiredConfirmations = Math.Max(1, (int)Math.Ceiling(total * requiredConfirmationPercentage / 100.0));

        var newsPost = new StfcNewsPost
        {
            Link = post.Link,
            Title = post.Title,
            PublishedAt = post.PublishedAt,
            DetectedAt = DateTimeOffset.UtcNow,
            EventGroup = post.EventGroup,
            RequiredConfirmations = requiredConfirmations,
        };
        db.StfcNewsPosts.Add(newsPost);
        await db.SaveChangesAsync(ct);

        foreach (var guildId in enabledGuildIds)
        {
            // The message lands in the guild-scoped AdminChannelId — that guild's language.
            var (embed, buttons) = StfcNewsMessageBuilder.Build(newsPost, confirmationCount: 0, await languageResolver.ForGuildAsync(guildId));
            var sent = await dispatcher.SendToAdminChannelAsync(guildId, embed: embed, buttons: buttons);
            if (sent is not { } message)
                continue;

            db.StfcNewsPostGuildMessages.Add(new StfcNewsPostGuildMessage
            {
                StfcNewsPostId = newsPost.Id,
                GuildId = guildId,
                ChannelId = message.ChannelId,
                MessageId = message.MessageId,
                EligibleMemberCount = eligibleCountsByGuild[guildId],
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // Role-count proxy for "how many people could plausibly confirm" — counts distinct
    // guild members holding CommandStaffRoleId and/or any GuildAdminRole, rather than
    // computing true per-channel visibility (permission overwrites + role math), which would
    // need new effective-permission code this app doesn't have. Assumes the guild's actual
    // AdminChannelId overwrites match one of these roles — not verified anywhere in the app
    // today. Falls back to 1 if neither role is configured, so a guild never zeroes out of
    // the pool entirely. Requires GatewayIntents.GuildMembers (see Program.cs) and the
    // Server Members privileged intent enabled in the Discord Developer Portal.
    private async Task<int> ComputeEligibleMemberCountAsync(ulong guildId, CancellationToken ct)
    {
        var settings = await db.GuildSettings.FindAsync([guildId], ct);

        var adminRoleIds = new HashSet<ulong>();
        if (settings?.CommandStaffRoleId is { } commandStaffRoleId)
            adminRoleIds.Add(commandStaffRoleId);
        adminRoleIds.UnionWith(await db.GuildAdminRoles
            .Where(r => r.GuildId == guildId)
            .Select(r => r.DiscordRoleId)
            .ToListAsync(ct));

        if (adminRoleIds.Count == 0)
            return 1;

        var count = 0;
        await foreach (var member in gatewayClient.Rest.GetGuildUsersAsync(guildId))
        {
            if (member.RoleIds.Any(adminRoleIds.Contains))
                count++;
        }

        return count;
    }

    private async Task<List<(string Title, string Link, DateTimeOffset? PublishedAt, string EventGroup)>> FetchMatchingPostsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(nameof(StfcNewsNotifyJob));
        var xml = await client.GetStringAsync(FeedUrl, ct);
        var doc = XDocument.Parse(xml);

        var results = new List<(string, string, DateTimeOffset?, string)>();
        foreach (var item in doc.Descendants("item"))
        {
            var title = item.Element("title")?.Value.Trim();
            var link = item.Element("link")?.Value.Trim();
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                continue;

            var eventGroup = TitleKeywordToEventGroup
                .FirstOrDefault(kv => title.Contains(kv.Keyword, StringComparison.OrdinalIgnoreCase))
                .EventGroup;
            if (eventGroup is null)
                continue;

            DateTimeOffset? publishedAt = DateTimeOffset.TryParse(
                item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;

            results.Add((title, link, publishedAt, eventGroup));
        }

        return results;
    }
}
