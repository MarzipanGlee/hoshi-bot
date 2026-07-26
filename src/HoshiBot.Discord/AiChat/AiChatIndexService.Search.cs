using System.Text;
using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace HoshiBot.Discord.AiChat;

// The retrieval side of the index: hybrid search (FTS + vector legs fused with Reciprocal Rank
// Fusion, recency and channel-tier weighting) plus the pre-index live-gather fallback. Index
// building/backfill/embedding lives in AiChatIndexService.cs; the shared channel/message plumbing
// in AiChatIndexService.Sources.cs.
public partial class AiChatIndexService
{
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

    private const int FallbackPerChannelLimit = 20;
    // Bound on how many resolved sources the FALLBACK live-gather stuffs into the prompt (used
    // only before a guild's index is first built) — keeps that prompt from exploding.
    private const int MaxKnowledgeSources = 25;

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

        // Search over the content PLUS the channel name, which for a forum/thread message holds the
        // thread title ("parent / Die Unsterblichkeits-Crew"). A forum post's title is often its most
        // important term and frequently appears nowhere in the body, so without this a title-only
        // query (e.g. "Unsterblichkeits-Crew") can't retrieve the post at all — only point at the
        // channel. The channel name is weighted 'A' and the body 'B' (setweight) so a TITLE match
        // ranks a post above rows that merely match a common query word in their body — otherwise a
        // specific post loses to recent/Preferred general chatter that happens to share a word like
        // "crew". Applies to existing rows immediately (no re-index); the vector leg gets the same
        // title context via EmbedPendingAsync's embed text.
        return await db.AiChatIndexedMessages
            .Where(m => m.GuildId == guildId
                && EF.Functions.ToTsVector(language, m.ChannelName ?? "").SetWeight('A')
                    .Concat(EF.Functions.ToTsVector(language, m.Content).SetWeight('B'))
                    .Matches(EF.Functions.WebSearchToTsQuery(language, search)))
            .OrderByDescending(m => EF.Functions.ToTsVector(language, m.ChannelName ?? "").SetWeight('A')
                .Concat(EF.Functions.ToTsVector(language, m.Content).SetWeight('B'))
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

    // Apostrophes are kept as part of a word (not a delimiter) so a proper noun like "V'GER"
    // survives as one specific token instead of shredding into "V" (dropped, too short) and
    // "GER" (kept, but generic) — the exact split that let an unrelated Gorn-crew thread
    // outrank the real V'GER answer, since only the generic fragment reached the query.
    [System.Text.RegularExpressions.GeneratedRegex(@"[^\p{L}\p{N}']+")]
    private static partial System.Text.RegularExpressions.Regex TokenSplitter();
}
