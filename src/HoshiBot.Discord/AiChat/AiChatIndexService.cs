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
    AiChatHealthService healthService,
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

    // Recency fusion: a third RRF term ranks the already-retrieved candidates newest-first, scaled by
    // this weight, so a fresher relevant row wins a near-tie over an older one that merely reads more
    // similarly (the general form of the promo-code/stale-content bug). Deliberately < 1 so it only
    // breaks near-ties, never overrides a clear relevance win — and applied only over the FTS/vector
    // candidate union, so it never pulls in recent-but-irrelevant chatter and can't bury a lone
    // evergreen hit (nothing fresher competes with it). Tunable.
    private const double RecencyWeight = 0.5;

    // Per-channel knowledge priority tiers (soft down-rank): after RRF fusion a candidate's score is
    // multiplied by its channel's tier factor, so Preferred sources win ties/near-ties and LastResort
    // sources only surface when nothing better matches. Values are tunable.
    private const double PreferredBoost = 1.5;
    private const double LastResortPenalty = 0.25;

    // The three knowledge buckets whose union is indexed; AiChatKnowledge is the Normal tier.
    private static readonly GuildFeature[] KnowledgeBuckets =
        [GuildFeature.AiChatKnowledge, GuildFeature.AiChatKnowledgePreferred, GuildFeature.AiChatKnowledgeLastResort];

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
    // CreatedAt drives the recency fusion term (see SearchAsync).
    private readonly record struct Candidate(int Id, ulong ChannelId, string? ChannelName, string Content, DateTimeOffset CreatedAt);

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

        // Reciprocal Rank Fusion: a row's score is the sum of 1/(RrfK + rank) across the lists, so a
        // hit ranked well by either leg — or moderately by both — floats to the top. The weight lets
        // the recency leg (below) contribute a fractional term.
        var scores = new Dictionary<int, double>();
        var byId = new Dictionary<int, Candidate>();
        void Fuse(IReadOnlyList<Candidate> list, double weight = 1.0)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var c = list[i];
                scores[c.Id] = scores.GetValueOrDefault(c.Id) + weight / (RrfK + i + 1);
                byId[c.Id] = c;
            }
        }
        Fuse(ftsCandidates);
        Fuse(vectorCandidates);

        // Recency leg: rank the ALREADY-retrieved candidate union newest-first and fuse it at a
        // fractional weight. Because it only reorders rows FTS/vector already surfaced, it nudges a
        // fresher relevant row above an older near-tie without introducing recent-but-irrelevant rows
        // or burying a lone evergreen hit (see RecencyWeight).
        var byRecency = byId.Values.OrderByDescending(c => c.CreatedAt).ToList();
        Fuse(byRecency, RecencyWeight);

        // Soft down-rank by channel priority tier: multiply each fused score by its tier factor, so
        // Preferred sources win ties/near-ties and LastResort sources only surface when nothing
        // better matches. A tier is resolved from the candidate's own channel first, then its parent
        // (category/forum) — so a child explicitly placed in a tier overrides its category's tier.
        var (preferred, lastResort) = await GetKnowledgePrioritySetsAsync(guildId);
        double TierFactor(Candidate c)
        {
            var parent = ResolveParentId(guildId, c.ChannelId);
            if (preferred.Contains(c.ChannelId) || (parent is { } pp && preferred.Contains(pp)))
                return PreferredBoost;
            if (lastResort.Contains(c.ChannelId) || (parent is { } pl && lastResort.Contains(pl)))
                return LastResortPenalty;
            return 1.0;
        }

        return scores
            .Select(kv => (Hit: byId[kv.Key], Score: kv.Value * TierFactor(byId[kv.Key])))
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => new KnowledgeHit(x.Hit.ChannelId, x.Hit.ChannelName, x.Hit.Content))
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
            .Select(m => new Candidate(m.Id, m.ChannelId, m.ChannelName, m.Content, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    // Semantic leg: embed the question and rank indexed rows by cosine distance. Skipped (empty)
    // when embeddings are disabled, none are stored yet, or the query-embed call fails.
    private async Task<List<Candidate>> VectorCandidatesAsync(HoshiBotDbContext db, ulong guildId, string queryText, CancellationToken cancellationToken)
    {
        if (!await embeddingService.IsEnabledAsync(guildId) || string.IsNullOrWhiteSpace(queryText))
            return [];

        var model = await embeddingService.GetModelAsync(guildId);

        // Cheap guard so we don't pay the query-embed cost before any row is embedded under the
        // guild's *currently* resolved model — rows embedded under a since-abandoned model (e.g.
        // before a guild switched embedding backend) don't count, matching the model filter below.
        var hasEmbeddings = await db.AiChatIndexedMessages
            .AnyAsync(m => m.GuildId == guildId && m.Embedding != null && m.EmbeddingModel == model, cancellationToken);
        if (!hasEmbeddings)
            return [];

        if (await embeddingService.EmbedAsync(guildId, queryText, cancellationToken) is not { } queryVec)
            return [];

        // Filtering by EmbeddingModel matters here in a way it wouldn't for a same-family model bump:
        // an Ollama vector and a Gemini vector are different coordinate systems entirely, so cosine
        // distance across them is close to meaningless, not just "a bit stale". Rows embedded under a
        // different model than the guild's current choice are excluded until EmbedPendingAsync's
        // stale-model detection re-embeds them, rather than polluting ranking with incomparable hits.
        return await db.AiChatIndexedMessages
            .Where(m => m.GuildId == guildId && m.Embedding != null && m.EmbeddingModel == model)
            .OrderBy(m => m.Embedding!.CosineDistance(queryVec))
            .Take(CandidatePoolSize)
            .Select(m => new Candidate(m.Id, m.ChannelId, m.ChannelName, m.Content, m.CreatedAt))
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
        // Index other authors' messages including webhooks/bots (crossposted official announcements
        // are valuable knowledge) — but never the bot's own messages (would be circular).
        if (message.GuildId is not { } guildId || message.Author.Id == gatewayClient.Id)
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
        // Never index the bot's own messages — otherwise she ingests her own past answers as
        // "knowledge" and cites them back to herself (a self-confirming loop, e.g. re-stating an
        // earlier wrong reply). This covers the live single-message path (MaybeIndexIncomingAsync);
        // backfill's own upsert path (UpsertMessagesAsync) has the same guard separately.
        if (message.Author.Id == gatewayClient.Id)
            return;

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
            // A real content change (an edit) invalidates the stored embedding — drop it so the embed
            // pass regenerates it from the new text, instead of leaving a vector for the old content.
            if (existing.Content != content)
            {
                existing.Content = content;
                existing.Embedding = null;
                existing.EmbeddingModel = null;
            }
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

    // Prune indexed rows for messages deleted in Discord (single or bulk). No-op for ids that were
    // never indexed (non-knowledge channels, bot messages, etc.).
    public async Task RemoveIndexedMessagesAsync(IReadOnlyCollection<ulong> messageIds, CancellationToken cancellationToken)
    {
        if (messageIds.Count == 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.AiChatIndexedMessages
            .Where(m => messageIds.Contains(m.MessageId))
            .ExecuteDeleteAsync(cancellationToken);
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
            // Never index the bot's own messages here either — IndexMessageAsync already guards the
            // live/single-message path, but backfill pages every author's messages via this method, so
            // without this check Hoshi's own past (possibly wrong) answers get ingested as "knowledge"
            // and cited back to herself.
            .Where(m => m.Author.Id != gatewayClient.Id)
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
                // Content changed (edit caught by the recent-catch-up pass) → drop the stale embedding.
                if (row.Content != x.Text)
                {
                    row.Content = x.Text;
                    row.Embedding = null;
                    row.EmbeddingModel = null;
                }
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
        if (!await embeddingService.IsEnabledAsync(guildId))
            return;

        var model = await embeddingService.GetModelAsync(guildId);
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
        string? failMessage = null;
        for (var i = 0; i < pending.Count; i += EmbedBatchSize)
        {
            var batch = pending.Skip(i).Take(EmbedBatchSize).ToList();
            var result = await embeddingService.EmbedBatchDetailedAsync(guildId, batch.Select(m => m.Content).ToList(), cancellationToken);

            var any = false;
            for (var j = 0; j < batch.Count; j++)
            {
                if (result.Vectors[j] is { } v)
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
            {
                failMessage = result.Error;
                break;
            }
        }

        // Record backend health so an operator sees an embed outage (e.g. a quota/billing failure)
        // from the Web admin instead of the logs. Both a success and a failure can be recorded in one
        // run (some batches embedded, then a later one hit a quota) — they set different fields.
        if (embedded > 0)
            await healthService.RecordSuccessAsync(guildId, AiChatProviderCallKind.Embed, model, cancellationToken);
        if (failMessage is not null)
            await healthService.RecordErrorAsync(guildId, AiChatProviderCallKind.Embed, model, failMessage, cancellationToken);

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
        // The knowledge buckets are storage-only, keyed off AiChat's enabled audiences. The indexed
        // set is the union of all three priority tiers (Normal/Preferred/LastResort).
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);
        var configured = new List<ulong>();
        foreach (var audience in enabledAudiences)
            foreach (var bucket in KnowledgeBuckets)
                configured.AddRange(await channelService.GetChannelsAsync(guildId, bucket, audience));
        return configured.Distinct().ToList();
    }

    // The configured channel/category ids for the Preferred and LastResort tiers (across AiChat's
    // enabled audiences). Everything else configured is Normal. Used by SearchAsync to weight hits.
    private async Task<(HashSet<ulong> Preferred, HashSet<ulong> LastResort)> GetKnowledgePrioritySetsAsync(ulong guildId)
    {
        var enabledAudiences = await featureService.GetEnabledAudiencesAsync(guildId, GuildFeature.AiChat);
        var preferred = new HashSet<ulong>();
        var lastResort = new HashSet<ulong>();
        foreach (var audience in enabledAudiences)
        {
            preferred.UnionWith(await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledgePreferred, audience));
            lastResort.UnionWith(await channelService.GetChannelsAsync(guildId, GuildFeature.AiChatKnowledgeLastResort, audience));
        }
        return (preferred, lastResort);
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

    // Apostrophes are kept as part of a word (not a delimiter) so a proper noun like "V'GER"
    // survives as one specific token instead of shredding into "V" (dropped, too short) and
    // "GER" (kept, but generic) — the exact split that let an unrelated Gorn-crew thread
    // outrank the real V'GER answer, since only the generic fragment reached the query.
    [System.Text.RegularExpressions.GeneratedRegex(@"[^\p{L}\p{N}']+")]
    private static partial System.Text.RegularExpressions.Regex TokenSplitter();
}
