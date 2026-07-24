using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Data;

// Reads/writes the latest AI backend health per (guild, call kind). The bot is the only writer —
// AiChatService records chat outcomes, AiChatIndexService records embedding outcomes — and the Web
// admin health page reads it. Lives in HoshiBot.Data so both the Discord bot and the Web app can use
// it without a cross-project reference. Best-effort telemetry: a lost update (concurrent first
// insert) is swallowed rather than surfaced, since it never affects bot behavior.
public class AiChatHealthService(IDbContextFactory<HoshiBotDbContext> dbFactory)
{
    private const int MaxMessageLength = 1000;

    public Task RecordSuccessAsync(ulong guildId, AiChatProviderCallKind kind, string? model, CancellationToken cancellationToken = default) =>
        UpsertAsync(guildId, kind, row =>
        {
            row.LastSuccessAt = DateTimeOffset.UtcNow;
            row.Model = model;
        }, cancellationToken);

    public Task RecordErrorAsync(ulong guildId, AiChatProviderCallKind kind, string? model, string? message, CancellationToken cancellationToken = default) =>
        UpsertAsync(guildId, kind, row =>
        {
            row.LastErrorAt = DateTimeOffset.UtcNow;
            row.LastErrorMessage = Truncate(message);
            row.Model = model;
        }, cancellationToken);

    public async Task<IReadOnlyList<AiChatProviderHealth>> GetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AiChatProviderHealths
            .AsNoTracking()
            .Where(h => h.GuildId == guildId)
            .ToListAsync(cancellationToken);
    }

    private async Task UpsertAsync(ulong guildId, AiChatProviderCallKind kind, Action<AiChatProviderHealth> apply, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.AiChatProviderHealths.FirstOrDefaultAsync(h => h.GuildId == guildId && h.Kind == kind, cancellationToken);
        if (row is null)
        {
            row = new AiChatProviderHealth { GuildId = guildId, Kind = kind };
            db.AiChatProviderHealths.Add(row);
        }

        apply(row);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent first insert for the same (guild, kind) lost the unique-index race — the
            // other writer stored it. Harmless for best-effort telemetry.
        }
    }

    private static string? Truncate(string? message) =>
        message is { Length: > MaxMessageLength } ? message[..MaxMessageLength] : message;
}
