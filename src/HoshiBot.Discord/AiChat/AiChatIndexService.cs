using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

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
//
// Partials: this file is the index-building side (live indexing, progressive backfill, embedding
// pass); AiChatIndexService.Search.cs is the retrieval side (hybrid FTS+vector search);
// AiChatIndexService.Sources.cs is the shared Discord source/fetch/render plumbing.
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

    // The backfill JOB must index every source — a guild whose knowledge is a few categories/forums
    // easily expands past 25 channels+threads, and anything beyond the cap silently never gets
    // indexed. This high safety limit only guards against a pathological forum with thousands of
    // threads.
    private const int MaxBackfillSources = 500;

    private const int MaxContentLength = 4000;

    // Embedding-pass pacing (config-tunable): how many messages per Ollama /api/embed call, and the
    // ceiling of messages embedded per guild per job run so the (CPU-only) embedder isn't swamped.
    private int EmbedBatchSize => configuration.GetValue<int?>("Ollama:EmbedBatchSize") ?? 50;
    private int MaxEmbedPerRun => configuration.GetValue<int?>("Ollama:MaxEmbedPerRun") ?? 1000;

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
                AuthorName = CommanderName.Of(gatewayClient, guildId, message.Author),
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
            existing.AuthorName = CommanderName.Of(gatewayClient, guildId, message.Author);
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

            // Null means the fetch failed, not that the channel is empty. Skip this source entirely
            // rather than reasoning from a count we never got — see the history leg below.
            if (recent is null)
                continue;

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

            // A failed page must NOT be read as "we reached the start of the channel". It used to
            // be: the helper returned an empty list on any RestException, 0 < 300 said "complete",
            // and one 403 or one transient 429 permanently ended that channel's backfill — silent,
            // unrecoverable, and logged only at debug level. Leave the cursor untouched and try
            // again next run.
            if (older is null)
                continue;

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
                row.AuthorName = CommanderName.Of(gatewayClient, guildId, x.Msg.Author);
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
                    AuthorName = CommanderName.Of(gatewayClient, guildId, x.Msg.Author),
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
            var result = await embeddingService.EmbedBatchDetailedAsync(guildId, batch.Select(EmbedText).ToList(), cancellationToken);

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

    // The text embedded for a row: the channel name (which for a forum/thread message carries the
    // thread title, often the post's most important term and absent from the body) prepended to the
    // content, so the semantic leg gains that title/topic context — the vector counterpart to
    // FtsCandidatesAsync also searching the channel name. Rows already embedded keep their old vector
    // until naturally re-embedded (content edit / model switch); new and re-embedded rows get this.
    private static string EmbedText(AiChatIndexedMessage m) =>
        string.IsNullOrWhiteSpace(m.ChannelName) ? m.Content : $"{m.ChannelName}\n{m.Content}";

    private static string Truncate(string text) =>
        text.Length <= MaxContentLength ? text : text[..MaxContentLength];
}
