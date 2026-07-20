using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace HoshiBot.Data;

// Store + recall for Hoshi's memories (GuildMemory). Formation (distilling text into memories) and
// embedding happen in the Discord layer — this owns the DB side: dedup-aware add, similarity recall,
// reinforcement, decay/prune, and the admin CRUD. Callers pass a precomputed embedding Vector (the
// embedding model lives in HoshiBot.Discord). Kept NetCord-free so HoshiBot.Web can use it too.
// See the memory plan in docs.
public class MemoryService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    // Cosine distance below which a candidate is treated as "we already remember this" — reinforce
    // the existing memory instead of storing a near-duplicate.
    private const double DuplicateDistance = 0.15;

    // How many nearest memories to pull before re-ranking by salience/recency.
    private const int PoolSize = 24;

    // Episodic memories most relevant to queryVec, re-ranked so semantic relevance leads (the cosine
    // pre-filter) while salience and recency break ties. Reinforcement is the caller's job.
    public async Task<List<GuildMemory>> SearchEpisodicAsync(ulong guildId, Vector queryVec, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var pool = await db.GuildMemories
            .Where(m => m.GuildId == guildId && m.Scope == MemoryScope.Episodic && m.Embedding != null)
            .OrderBy(m => m.Embedding!.CosineDistance(queryVec))
            .Take(PoolSize)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return pool.OrderByDescending(m => RerankScore(m, now)).Take(limit).ToList();
    }

    // A few always-worth-mentioning recent + salient memories, independent of any query, so the
    // injected block isn't empty when nothing matches semantically.
    public async Task<List<GuildMemory>> GetRecentSalientAsync(ulong guildId, MemoryScope scope, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.GuildMemories
            .Where(m => m.GuildId == guildId && m.Scope == scope)
            .OrderByDescending(m => m.Salience)
            .ThenByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private static double RerankScore(GuildMemory m, DateTimeOffset now)
    {
        var ageDays = (now - m.CreatedAt).TotalDays;
        var recency = 1.0 / (1.0 + ageDays / 30.0); // ~half weight by ~30 days old
        return m.Salience + recency;
    }

    // Store a new memory unless a very similar one already exists — in which case bump that one's
    // salience/recall (reinforcement), so repeated mentions strengthen a memory rather than duplicate it.
    public async Task AddIfNovelAsync(GuildMemory memory, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (memory.Embedding is { } vec)
        {
            var nearest = await db.GuildMemories
                .Where(m => m.GuildId == memory.GuildId && m.Scope == memory.Scope && m.Embedding != null)
                .Select(m => new { Memory = m, Distance = m.Embedding!.CosineDistance(vec) })
                .OrderBy(x => x.Distance)
                .FirstOrDefaultAsync(cancellationToken);
            if (nearest is not null && nearest.Distance < DuplicateDistance)
            {
                nearest.Memory.Salience = Math.Min(5, nearest.Memory.Salience + 1);
                nearest.Memory.LastRecalledAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        db.GuildMemories.Add(memory);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Phase 2 (conversation memory): store a per-channel conversation snapshot, then keep only the
    // newest `keepPerChannel` for that channel — recency is the decay here, and consecutive snapshots
    // on one topic are meant to be distinct time points (so no dedup, unlike episodic).
    public async Task AddConversationSnapshotAsync(GuildMemory memory, int keepPerChannel, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.GuildMemories.Add(memory);
        await db.SaveChangesAsync(cancellationToken);

        var stale = await db.GuildMemories
            .Where(m => m.GuildId == memory.GuildId && m.Scope == MemoryScope.Conversation && m.ChannelId == memory.ChannelId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(keepPerChannel)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        if (stale.Count > 0)
            await db.GuildMemories.Where(m => stale.Contains(m.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    // The current channel's most recent conversation snapshots, oldest→newest for the prompt.
    public async Task<List<GuildMemory>> GetRecentConversationAsync(ulong guildId, ulong channelId, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var recent = await db.GuildMemories
            .Where(m => m.GuildId == guildId && m.Scope == MemoryScope.Conversation && m.ChannelId == channelId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        recent.Reverse();
        return recent;
    }

    // Reinforcement: mark the memories that were actually recalled into a prompt as recently used, so
    // genuinely useful ones survive decay while never-recalled ones fade.
    public async Task ReinforceAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await db.GuildMemories
            .Where(m => idList.Contains(m.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastRecalledAt, now), cancellationToken);
    }

    // Forget old, unimportant, never-recalled episodic memories so the store stays lean.
    public async Task<int> PruneAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);
        return await db.GuildMemories
            .Where(m => m.GuildId == guildId && m.Scope == MemoryScope.Episodic
                && m.Salience <= 1 && m.CreatedAt < cutoff
                && (m.LastRecalledAt == null || m.LastRecalledAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);
    }

    // --- Admin (staff review/edit/forget) ---

    public async Task<List<GuildMemory>> GetForGuildAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.GuildMemories
            .Where(m => m.GuildId == guildId)
            .OrderByDescending(m => m.Salience)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(int id, string content, int salience, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var memory = await db.GuildMemories.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (memory is null)
            return;
        memory.Content = content.Trim();
        memory.Salience = Math.Clamp(salience, 1, 5);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.GuildMemories.Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}
