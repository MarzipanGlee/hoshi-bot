using System.Text;
using HoshiBot.Domain.Entities;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord.AiChat;

// The Discord-facing plumbing shared by both the indexing and the search side: resolving the
// configured knowledge sources (categories → children, forums → threads), fetching message pages
// via REST, rendering a message's readable text, and the gateway-cache channel/thread lookups.
public partial class AiChatIndexService
{
    // The three knowledge buckets whose union is indexed; AiChatKnowledge is the Normal tier.
    private static readonly GuildFeature[] KnowledgeBuckets =
        [GuildFeature.AiChatKnowledge, GuildFeature.AiChatKnowledgePreferred, GuildFeature.AiChatKnowledgeLastResort];

    private const int ForumArchivedThreadLimit = 10;

    private async Task<List<ulong>> GetConfiguredKnowledgeChannelIdsAsync(ulong guildId)
    {
        // The knowledge buckets are storage-only, keyed off AiChat's enabled audiences. The indexed
        // set is the union of all three priority tiers (Normal/Preferred/LastResort).
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);
        var configured = new List<ulong>();
        foreach (var audience in enabledAudiences)
            foreach (var bucket in KnowledgeBuckets)
                configured.AddRange(await channelService.GetChannelsAsync(guildId, bucket, audience));
        return configured.Distinct().ToList();
    }

    // Resolves configured entries to concrete message sources with a display name: categories to
    // their text/forum children, forums to their threads ("Forum / Thread"), text channels to
    // themselves; voice/stage contribute nothing.
    private async Task<List<(ulong Id, string? Name)>> ResolveSourcesAsync(ulong guildId, List<ulong> configured, CancellationToken cancellationToken)
    {
        var all = await GetGuildChannelsAsync(guildId, cancellationToken);
        var byId = all.ToDictionary(c => c.Id);
        var resolved = new List<(ulong Id, string? Name)>();

        foreach (var id in configured)
        {
            if (byId.GetValueOrDefault(id) is CategoryGuildChannel)
            {
                foreach (var child in all.Where(c => ParentIdOf(c) == id))
                    await AddSourceAsync(guildId, child, resolved, cancellationToken);
            }
            else if (byId.GetValueOrDefault(id) is { } channel)
            {
                await AddSourceAsync(guildId, channel, resolved, cancellationToken);
            }
            else
            {
                resolved.Add((id, null));
            }
        }

        return resolved.GroupBy(r => r.Id).Select(g => g.First()).ToList();
    }

    private async Task AddSourceAsync(ulong guildId, IGuildChannel channel, List<(ulong Id, string? Name)> resolved, CancellationToken cancellationToken)
    {
        switch (channel)
        {
            case ForumGuildChannel:
                resolved.AddRange(await GetForumThreadsAsync(guildId, channel.Id, channel.Name, cancellationToken));
                break;
            case VoiceGuildChannel or StageGuildChannel:
                break;
            case TextGuildChannel:
                resolved.Add((channel.Id, channel.Name));
                break;
        }
    }

    private async Task<List<(ulong Id, string? Name)>> GetForumThreadsAsync(ulong guildId, ulong forumId, string forumName, CancellationToken cancellationToken)
    {
        var threads = new List<(ulong Id, string? Name)>();
        try
        {
            var active = await gatewayClient.Rest.GetActiveGuildThreadsAsync(guildId, cancellationToken: cancellationToken);
            threads.AddRange(active.Where(t => t.ParentId == forumId).Select(t => (t.Id, (string?)$"{forumName} / {t.Name}")));
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not fetch active threads for forum {ForumId}", forumId);
        }

        try
        {
            var count = 0;
            await foreach (var thread in gatewayClient.Rest.GetPublicArchivedGuildThreadsAsync(forumId).WithCancellation(cancellationToken))
            {
                threads.Add((thread.Id, $"{forumName} / {thread.Name}"));
                if (++count >= ForumArchivedThreadLimit)
                    break;
            }
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not fetch archived threads for forum {ForumId}", forumId);
        }

        return threads;
    }

    // The subset of `ids` that GetMessagesAsync can actually be called on: plain text/announcement
    // channels. A category or forum id holds no messages of its own, so fetching from one is a
    // guaranteed-failing REST call — callers on the answer hot path (BuildLatestAnnouncementsBlock)
    // filter first instead. Expanding a forum to its threads here would be the wrong trade: N extra
    // REST calls per answer, when the forum's posts already reach answers through the index.
    // Unknown ids are kept, so a channel missing from the cache behaves as before.
    public async Task<List<ulong>> FilterDirectMessageSourcesAsync(ulong guildId, IEnumerable<ulong> ids, CancellationToken cancellationToken)
    {
        var byId = (await GetGuildChannelsAsync(guildId, cancellationToken)).ToDictionary(c => c.Id);
        return ids.Where(id => byId.GetValueOrDefault(id) is not { } channel
                || channel is TextGuildChannel and not (VoiceGuildChannel or StageGuildChannel))
            .ToList();
    }

    // Newest-first list of up to `limit` recent messages; empty on any REST error (missing
    // permissions etc. must never crash the message pump or the backfill job).
    public async Task<List<RestMessage>> FetchRecentAsync(ulong channelId, int limit, CancellationToken cancellationToken)
    {
        var messages = new List<RestMessage>();
        try
        {
            await foreach (var m in gatewayClient.Rest.GetMessagesAsync(channelId).WithCancellation(cancellationToken))
            {
                messages.Add(m);
                if (messages.Count >= limit)
                    break;
            }
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not fetch messages from channel {ChannelId}", channelId);
        }

        return messages;
    }

    // Pages BACKWARD (older) from beforeMessageId — or from the newest message when null — up to
    // `limit`. The backbone of the progressive history backfill. Empty on any REST error.
    public async Task<List<RestMessage>> FetchBeforeAsync(ulong channelId, ulong? beforeMessageId, int limit, CancellationToken cancellationToken)
    {
        var messages = new List<RestMessage>();
        var pagination = new PaginationProperties<ulong> { Direction = PaginationDirection.Before };
        if (beforeMessageId is { } id)
            pagination.From = id;

        try
        {
            await foreach (var m in gatewayClient.Rest.GetMessagesAsync(channelId, pagination).WithCancellation(cancellationToken))
            {
                messages.Add(m);
                if (messages.Count >= limit)
                    break;
            }
        }
        catch (RestException ex)
        {
            logger.LogDebug(ex, "Could not fetch messages before {Before} from channel {ChannelId}", beforeMessageId, channelId);
        }

        return messages;
    }

    // A message's readable text: its content plus any embed text (title/description/fields/
    // footer/author) — many info channels (RoE, rules, announcements) post ONLY embeds, so
    // reading just Content would see nothing there. Shared with AiChatService (memory turns).
    public static string RenderMessageText(RestMessage message)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(message.Content))
            sb.AppendLine(message.Content.Trim());

        foreach (var embed in message.Embeds)
        {
            if (!string.IsNullOrWhiteSpace(embed.Author?.Name))
                sb.AppendLine(embed.Author!.Name);
            if (!string.IsNullOrWhiteSpace(embed.Title))
                sb.AppendLine(embed.Title);
            if (!string.IsNullOrWhiteSpace(embed.Description))
                sb.AppendLine(embed.Description);
            foreach (var field in embed.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.Name) || !string.IsNullOrWhiteSpace(field.Value))
                    sb.AppendLine($"{field.Name}: {field.Value}");
            }
            if (!string.IsNullOrWhiteSpace(embed.Footer?.Text))
                sb.AppendLine(embed.Footer!.Text);
        }

        return sb.ToString().Trim();
    }

    private IReadOnlyList<IGuildChannel> GetGuildChannelsFromCache(ulong guildId) =>
        gatewayClient.Cache.Guilds.TryGetValue(guildId, out var g) && g.Channels.Count > 0
            ? g.Channels.Values.ToList()
            : [];

    private async Task<IReadOnlyList<IGuildChannel>> GetGuildChannelsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var cached = GetGuildChannelsFromCache(guildId);
        if (cached.Count > 0)
            return cached;
        try { return await gatewayClient.Rest.GetGuildChannelsAsync(guildId, cancellationToken: cancellationToken); }
        catch (RestException) { return []; }
    }

    // Forum posts are threads, and NetCord's gateway cache tracks those in a separate
    // ActiveThreads dictionary — never merged into Channels (confirmed against NetCord's
    // Guild ctor, which populates each from its own distinct JSON array). A lookup that only
    // checks Channels silently misses every forum thread, which would make a Preferred/
    // LastResort tier assigned to the forum itself never resolve for any of its posts (they'd
    // all quietly fall back to Normal weight) — so both lookups here also check ActiveThreads.
    private ulong? ResolveParentId(ulong guildId, ulong channelId)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out var g))
            return null;
        if (g.Channels.TryGetValue(channelId, out var ch))
            return ParentIdOf(ch);
        return g.ActiveThreads.TryGetValue(channelId, out var thread) ? thread.ParentId : null;
    }

    private string? ResolveChannelName(ulong guildId, ulong channelId)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(guildId, out var g))
            return null;
        if (g.Channels.TryGetValue(channelId, out var ch))
            return ch.Name;
        return g.ActiveThreads.TryGetValue(channelId, out var thread) ? thread.Name : null;
    }

    private static ulong? ParentIdOf(IGuildChannel channel) => channel switch
    {
        TextGuildChannel t => t.ParentId,
        ForumGuildChannel f => f.ParentId,
        _ => null,
    };
}
