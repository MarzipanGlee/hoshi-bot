using System.Text;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace HoshiBot.Discord.AiChat;

// The persistent search index behind AI-chat grounding. Knowledge-channel messages (content +
// embed text) are stored in AiChatIndexedMessage and matched per question with Postgres full-text
// search — so answers can draw on ALL indexed history, not just the most recent messages, without
// hitting Discord on every question.
//
// The search language is per guild (AiChatSettingKeys.SearchLanguage, default from the guild's
// Discord locale via FtsLanguage). Because a per-guild config can't live in a generated tsvector
// column, the vector is computed at query time with the guild's config.
//
// Populated two ways: live (AiChatMessageHandler indexes each incoming knowledge-channel message)
// and by AiChatIndexJob (periodic backfill of history/forums + re-index to catch edits). Both this
// service and AiChatService reuse RenderMessageText/FetchRecentAsync, which live here.
public partial class AiChatIndexService(
    IDbContextFactory<HoshiBotDbContext> dbFactory,
    GatewayClient gatewayClient,
    GuildFeatureService featureService,
    GuildFeatureChannelService channelService,
    AiChatEmbeddingService embeddingService,
    IConfiguration configuration,
    ILogger<AiChatIndexService> logger)
{
    // Discord's max messages per REST page — the unit of the progressive backfill's stepping.
    private const int HistoryPageSize = 100;
    // How many historical messages the backfill deepens per channel PER RUN. Bounds each run so a
    // large channel's full history fills in over many hourly runs rather than all at once.
    private const int MaxHistoryPerChannelPerRun = 300;
    // How many candidates each retrieval leg (FTS, vector) pulls before RRF fusion.
    private const int CandidatePoolSize = 40;
    // Reciprocal Rank Fusion constant (standard 60): score += 1 / (RrfK + rank).
    private const int RrfK = 60;

    private const int FallbackPerChannelLimit = 20;
    // Bound on how many resolved sources the FALLBACK live-gather stuffs into the prompt (used
    // only before a guild's index is first built) — keeps that prompt from exploding.
    private const int MaxKnowledgeSources = 25;

    // The backfill JOB must index every source — a guild whose knowledge is a few categories/forums
    // easily expands past 25 channels+threads, and anything beyond the cap silently never gets
    // indexed. This high safety limit only guards against a pathological forum with thousands of
    // threads.
    private const int MaxBackfillSources = 500;

    private const int ForumArchivedThreadLimit = 10;
    private const int MaxContentLength = 4000;

    // Embedding-pass pacing (config-tunable): how many messages per Ollama /api/embed call, and the
    // ceiling of messages embedded per guild per job run so the (CPU-only) embedder isn't swamped.
    private int EmbedBatchSize => configuration.GetValue<int?>("Ollama:EmbedBatchSize") ?? 50;
    private int MaxEmbedPerRun => configuration.GetValue<int?>("Ollama:MaxEmbedPerRun") ?? 1000;

    public readonly record struct KnowledgeHit(ulong ChannelId, string? ChannelName, string Content);

    // One retrieval candidate (from either the FTS or the vector leg), keyed by row Id for fusion.
    private readonly record struct Candidate(int Id, ulong ChannelId, string? ChannelName, string Content);

    // Hybrid search of this guild's index: keyword full-text search AND semantic vector search,
    // fused with Reciprocal Rank Fusion. FTS keeps exact-term/rare-word precision (proper nouns,
    // game jargon); the vector leg adds paraphrase/synonym recall. Degrades gracefully to FTS-only
    // when embeddings are absent or the embedder is unreachable.
    public async Task<List<KnowledgeHit>> SearchAsync(ulong guildId, string language, string queryText, int limit, CancellationToken cancellationToken)
    {
        language = FtsLanguage.Normalize(language);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var ftsCandidates = await FtsCandidatesAsync(db, guildId, language, queryText, cancellationToken);
        var vectorCandidates = await VectorCandidatesAsync(db, guildId, queryText, cancellationToken);

        if (ftsCandidates.Count == 0 && vectorCandidates.Count == 0)
            return [];

        // Reciprocal Rank Fusion: a row's score is the sum of 1/(RrfK + rank) across the two lists,
        // so a hit ranked well by either leg — or moderately by both — floats to the top.
        var scores = new Dictionary<int, double>();
        var byId = new Dictionary<int, Candidate>();
        void Fuse(List<Candidate> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var c = list[i];
                scores[c.Id] = scores.GetValueOrDefault(c.Id) + 1.0 / (RrfK + i + 1);
                byId[c.Id] = c;
            }
        }
        Fuse(ftsCandidates);
        Fuse(vectorCandidates);

        return scores
            .OrderByDescending(kv => kv.Value)
            .Take(limit)
            .Select(kv => byId[kv.Key])
            .Select(c => new KnowledgeHit(c.ChannelId, c.ChannelName, c.Content))
            .ToList();
    }

    // Keyword leg: a user's question is a whole sentence; websearch_to_tsquery ANDs its words, so
    // requiring every word to co-occur matches almost nothing. Turn it into an OR of the significant
    // terms (recall) and let ts_rank surface the best matches (precision).
    private async Task<List<Candidate>> FtsCandidatesAsync(HoshiBotDbContext db, ulong guildId, string language, string queryText, CancellationToken cancellationToken)
    {
        var search = ToOrQuery(queryText);
        if (string.IsNullOrWhiteSpace(search))
            return [];

        return await db.AiChatIndexedMessages
            .Where(m => m.GuildId == guildId
                && EF.Functions.ToTsVector(language, m.Content)
                    .Matches(EF.Functions.WebSearchToTsQuery(language, search)))
            .OrderByDescending(m => EF.Functions.ToTsVector(language, m.Content)
                .Rank(EF.Functions.WebSearchToTsQuery(language, search)))
            .ThenByDescending(m => m.CreatedAt)
            .Take(CandidatePoolSize)
            .Select(m => new Candidate(m.Id, m.ChannelId, m.ChannelName, m.Content))
            .ToListAsync(cancellationToken);
    }

    // Semantic leg: embed the question and rank indexed rows by cosine distance. Skipped (empty)
    // when embeddings are disabled, none are stored yet, or the query-embed call fails.
    private async Task<List<Candidate>> VectorCandidatesAsync(HoshiBotDbContext db, ulong guildId, string queryText, CancellationToken cancellationToken)
    {
        if (!embeddingService.Enabled || string.IsNullOrWhiteSpace(queryText))
            return [];

        // Cheap guard so we don't pay the query-embed cost before any row is embedded.
        var hasEmbeddings = await db.AiChatIndexedMessages
            .AnyAsync(m => m.GuildId == guildId && m.Embedding != null, cancellationToken);
        if (!hasEmbeddings)
            return [];

        if (await embeddingService.EmbedAsync(queryText, cancellationToken) is not { } queryVec)
            return [];

        return await db.AiChatIndexedMessages
            .Where(m => m.GuildId == guildId && m.Embedding != null)
            .OrderBy(m => m.Embedding!.CosineDistance(queryVec))
            .Take(CandidatePoolSize)
            .Select(m => new Candidate(m.Id, m.ChannelId, m.ChannelName, m.Content))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasIndexedContentAsync(ulong guildId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AiChatIndexedMessages.AnyAsync(m => m.GuildId == guildId, cancellationToken);
    }

    // Live entry point from the message handler: index the message iff the guild has AiChat on and
    // the message is in one of its knowledge sources. Cheap and safe to call for every message.
    public async Task MaybeIndexIncomingAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.GuildId is not { } guildId || message.Author.IsBot)
            return;
        if (message.Type is not (MessageType.Default or MessageType.Reply))
            return;
        if (!await featureService.IsEnabledAsync(guildId, GuildFeature.AiChat))
            return;

        var configured = (await GetConfiguredKnowledgeChannelIdsAsync(guildId)).ToHashSet();
        if (configured.Count == 0)
            return;

        var parentId = ResolveParentId(guildId, message.ChannelId);
        if (!configured.Contains(message.ChannelId) && !(parentId is { } p && configured.Contains(p)))
            return;

        await IndexMessageAsync(guildId, message, channelName: null, cancellationToken);
    }

    // Upsert one message by its Discord id.
    public async Task IndexMessageAsync(ulong guildId, RestMessage message, string? channelName, CancellationToken cancellationToken)
    {
        var content = Truncate(RenderMessageText(message));
        if (string.IsNullOrWhiteSpace(content))
            return;

        channelName ??= ResolveChannelName(guildId, message.ChannelId);
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.AiChatIndexedMessages.FirstOrDefaultAsync(m => m.MessageId == message.Id, cancellationToken);
        if (existing is null)
        {
            db.AiChatIndexedMessages.Add(new AiChatIndexedMessage
            {
                GuildId = guildId,
                ChannelId = message.ChannelId,
                MessageId = message.Id,
                ChannelName = channelName,
                AuthorName = CommanderName.Of(message.Author),
                Content = content,
                CreatedAt = message.CreatedAt,
                IndexedAt = now,
            });
        }
        else
        {
            existing.Content = content;
            existing.ChannelName = channelName ?? existing.ChannelName;
            existing.AuthorName = CommanderName.Of(message.Author);
            existing.IndexedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent live/backfill insert of the same message id can lose the unique-index
            // race — harmless, the other writer stored it.
            logger.LogDebug(ex, "AiChat index upsert conflict for message {MessageId}", message.Id);
        }
    }

    // Progressive full-history backfill for one guild. Per knowledge source, each run does two
    // bounded passes: a recent catch-up (newest page, for new messages + edits) and a history
    // deepening step that pages BACKWARD from the oldest already-indexed message, a capped chunk at
    // a time, until the channel's start is reached (recorded in AiChatBackfillState so completed
    // channels stop paging). This indexes the entire history over many runs without hammering
    // Discord's REST all at once.
    public async Task BackfillGuildAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var configured = await GetConfiguredKnowledgeChannelIdsAsync(guildId);
        if (configured.Count == 0)
            return;

        var sources = (await ResolveSourcesAsync(guildId, configured, cancellationToken)).Take(MaxBackfillSources).ToList();
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var cursors = (await db.AiChatBackfillStates.Where(s => s.GuildId == guildId).ToListAsync(cancellationToken))
            .ToDictionary(s => s.ChannelId);

        // Guard against adding the same MessageId twice within one run (a message shared by two
        // sources, or a recent/history overlap) — a duplicate INSERT would fail the whole save.
        var seen = new HashSet<ulong>();
        int recentCount = 0, historyCount = 0, completedCount = 0;

        foreach (var (channelId, channelName) in sources)
        {
            // 1. Recent catch-up: newest page (new messages + recent edits).
            var recent = await FetchRecentAsync(channelId, HistoryPageSize, cancellationToken);
            recentCount += await UpsertMessagesAsync(db, guildId, channelId, channelName, recent, now, seen, cancellationToken);

            var cursor = cursors.GetValueOrDefault(channelId);
            if (cursor?.HistoryComplete == true)
                continue;

            // 2. History deepening: page backward from the oldest message we know of (DB min, or the
            // recent page's oldest on the first run) so we don't re-fetch the recent page.
            var dbAnchor = await db.AiChatIndexedMessages
                .Where(m => m.GuildId == guildId && m.ChannelId == channelId)
                .MinAsync(m => (ulong?)m.MessageId, cancellationToken);
            ulong? anchor = dbAnchor;
            if (recent.Count > 0)
            {
                var recentMin = recent.Min(m => m.Id);
                anchor = anchor is null ? recentMin : Math.Min(anchor.Value, recentMin);
            }

            var older = await FetchBeforeAsync(channelId, anchor, MaxHistoryPerChannelPerRun, cancellationToken);
            historyCount += await UpsertMessagesAsync(db, guildId, channelId, channelName, older, now, seen, cancellationToken);

            // Fewer than the per-run cap came back ⇒ no older messages remain ⇒ channel is complete.
            var reachedStart = older.Count < MaxHistoryPerChannelPerRun;
            if (cursor is null)
            {
                cursor = new AiChatBackfillState { GuildId = guildId, ChannelId = channelId };
                db.AiChatBackfillStates.Add(cursor);
                cursors[channelId] = cursor;
            }
            cursor.HistoryComplete = reachedStart;
            cursor.UpdatedAt = now;
            if (reachedStart)
                completedCount++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogDebug(ex, "AiChat index backfill upsert conflict for guild {GuildId}", guildId);
        }

        logger.LogInformation(
            "AiChat backfill guild {Guild}: {Sources} sources, {Recent} recent + {History} historical messages indexed, {Completed} channels reached start",
            guildId, sources.Count, recentCount, historyCount, completedCount);
    }

    // Renders + upserts a batch of fetched messages into the given (tracked) context; returns how
    // many were handled. `seen` dedupes within a run so the single SaveChanges can't hit a duplicate
    // insert. Caller saves.
    private async Task<int> UpsertMessagesAsync(HoshiBotDbContext db, ulong guildId, ulong channelId, string? channelName, List<RestMessage> messages, DateTimeOffset now, HashSet<ulong> seen, CancellationToken cancellationToken)
    {
        var rendered = messages
            .Where(m => seen.Add(m.Id))
            .Select(m => new { Msg = m, Text = Truncate(RenderMessageText(m)) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToList();
        if (rendered.Count == 0)
            return 0;

        var ids = rendered.Select(x => x.Msg.Id).ToList();
        var existingById = (await db.AiChatIndexedMessages.Where(m => ids.Contains(m.MessageId)).ToListAsync(cancellationToken))
            .ToDictionary(m => m.MessageId);

        foreach (var x in rendered)
        {
            if (existingById.TryGetValue(x.Msg.Id, out var row))
            {
                row.Content = x.Text;
                row.ChannelName = channelName ?? row.ChannelName;
                row.AuthorName = CommanderName.Of(x.Msg.Author);
                row.IndexedAt = now;
            }
            else
            {
                db.AiChatIndexedMessages.Add(new AiChatIndexedMessage
                {
                    GuildId = guildId,
                    ChannelId = channelId,
                    MessageId = x.Msg.Id,
                    ChannelName = channelName,
                    AuthorName = CommanderName.Of(x.Msg.Author),
                    Content = x.Text,
                    CreatedAt = x.Msg.CreatedAt,
                    IndexedAt = now,
                });
            }
        }

        return rendered.Count;
    }

    // Embeds this guild's un-embedded (or stale-model) indexed messages in batches, up to a per-run
    // cap so the CPU-only embedder isn't swamped — the rest catch up on later runs. No-op when
    // semantic search is disabled. Newest messages first (most likely to be queried).
    public async Task EmbedPendingAsync(ulong guildId, CancellationToken cancellationToken)
    {
        if (!embeddingService.Enabled)
            return;

        var model = embeddingService.Model;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var maxPerRun = MaxEmbedPerRun;
        var pending = await db.AiChatIndexedMessages
            .Where(m => m.GuildId == guildId && (m.Embedding == null || m.EmbeddingModel != model))
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxPerRun + 1)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
            return;

        var capped = pending.Count > maxPerRun;
        if (capped)
            pending = pending.Take(maxPerRun).ToList();

        var embedded = 0;
        for (var i = 0; i < pending.Count; i += EmbedBatchSize)
        {
            var batch = pending.Skip(i).Take(EmbedBatchSize).ToList();
            var vectors = await embeddingService.EmbedBatchAsync(batch.Select(m => m.Content).ToList(), cancellationToken);

            var any = false;
            for (var j = 0; j < batch.Count; j++)
            {
                if (vectors[j] is { } v)
                {
                    batch[j].Embedding = v;
                    batch[j].EmbeddingModel = model;
                    embedded++;
                    any = true;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            // Whole batch failed ⇒ embedder is down; stop hammering it this run.
            if (!any)
                break;
        }

        if (embedded > 0 || capped)
            logger.LogInformation(
                "AiChat embedding guild {Guild}: embedded {Embedded} messages (model {Model}){Capped}",
                guildId, embedded, model, capped ? $"; per-run cap {maxPerRun} hit, more remain" : "");
    }

    // Zero-regression path used before the index has any content for a guild: the old "gather
    // recent messages of every knowledge source" behavior, formatted as prompt lines.
    public async Task<string> GetRecentKnowledgeFallbackAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var configured = await GetConfiguredKnowledgeChannelIdsAsync(guildId);
        if (configured.Count == 0)
            return "";

        var sources = await ResolveSourcesAsync(guildId, configured, cancellationToken);
        var sb = new StringBuilder();
        foreach (var (channelId, channelName) in sources.Take(MaxKnowledgeSources))
        {
            var messages = await FetchRecentAsync(channelId, FallbackPerChannelLimit, cancellationToken);
            messages.Reverse();
            foreach (var m in messages)
            {
                var text = RenderMessageText(m);
                if (!string.IsNullOrEmpty(text))
                    sb.AppendLine($"- [<#{channelId}>] {text}");
            }
        }

        return sb.ToString();
    }

    private async Task<List<ulong>> GetConfiguredKnowledgeChannelIdsAsync(ulong guildId)
    {
        // AiChatKnowledge is a storage-only bucket keyed off AiChat's enabled audiences.
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);
        var configured = new List<ulong>();
        foreach (var audience in enabledAudiences)
            configured.AddRange(await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledge, audience));
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

    private ulong? ResolveParentId(ulong guildId, ulong channelId) =>
        gatewayClient.Cache.Guilds.TryGetValue(guildId, out var g) && g.Channels.TryGetValue(channelId, out var ch)
            ? ParentIdOf(ch)
            : null;

    private string? ResolveChannelName(ulong guildId, ulong channelId) =>
        gatewayClient.Cache.Guilds.TryGetValue(guildId, out var g) && g.Channels.TryGetValue(channelId, out var ch)
            ? ch.Name
            : null;

    private static ulong? ParentIdOf(IGuildChannel channel) => channel switch
    {
        TextGuildChannel t => t.ParentId,
        ForumGuildChannel f => f.ParentId,
        _ => null,
    };

    // Turns a free-text question into an OR of its significant terms for websearch_to_tsquery
    // (which otherwise ANDs them). "or"/"and" are dropped so they aren't taken as operators;
    // per-term stemming/stopword removal is still handled by the text-search config.
    private static string ToOrQuery(string text)
    {
        var terms = TokenSplitter().Split(text)
            .Where(t => t.Length >= 3
                && !t.Equals("or", StringComparison.OrdinalIgnoreCase)
                && !t.Equals("and", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(" or ", terms);
    }

    private static string Truncate(string text) =>
        text.Length <= MaxContentLength ? text : text[..MaxContentLength];

    [System.Text.RegularExpressions.GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial System.Text.RegularExpressions.Regex TokenSplitter();
}
