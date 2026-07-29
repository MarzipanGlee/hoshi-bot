using HoshiBot.Data;
using HoshiBot.Domain.Entities;
using HoshiBot.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;

namespace HoshiBot.Discord;

// Backs the "Zurück"/"Abbrechen" retry step shown when a modal's freeform input fails
// validation (e.g. an unknown system name) — see PendingModalInput for why this is a DB
// row instead of custom-id-encoded state.
public class PendingModalInputService(HoshiBotDbContext db)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    // The retry message these buttons sit on is ephemeral to the user whose modal input
    // failed — callers pass that acting user's resolved language.
    // ↩️ rather than 🔙 — the "BACK" glyph renders as small dark text that washes out on the grey
    // secondary button; a plain return arrow reads clearly.
    public static ButtonProperties BackButton(int id, Language lang) =>
        new($"modal-retry-back:{id}", Msg.Bridge.BackButton(lang), EmojiProperties.Standard("↩️"), ButtonStyle.Secondary);

    public static ButtonProperties CancelButton(int id, Language lang) =>
        new($"modal-retry-cancel:{id}", Msg.Bridge.CancelButton(lang), EmojiProperties.Standard("✖️"), ButtonStyle.Danger);

    public async Task<int> CreateAsync(ulong guildId, ulong userId, PendingModalInputKind kind,
        string? field1 = null, string? field2 = null, string? field3 = null, string? field4 = null)
    {
        var pending = new PendingModalInput
        {
            GuildId = guildId,
            DiscordUserId = userId,
            Kind = kind,
            Field1 = field1,
            Field2 = field2,
            Field3 = field3,
            Field4 = field4,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.PendingModalInputs.Add(pending);
        await db.SaveChangesAsync();

        return pending.Id;
    }

    public async Task<PendingModalInput?> GetAsync(int id, ulong callerId)
    {
        var pending = await db.PendingModalInputs.FindAsync(id);
        return pending is not null && pending.DiscordUserId == callerId ? pending : null;
    }

    public async Task DeleteAsync(int id)
    {
        var pending = await db.PendingModalInputs.FindAsync(id);
        if (pending is not null)
        {
            db.PendingModalInputs.Remove(pending);
            await db.SaveChangesAsync();
        }
    }

    // Abandoned retries (error shown, user never clicked Zurück or Abbrechen) — same TTL
    // spirit as Absences' stale-draft sweep.
    public async Task SweepStaleAsync()
    {
        var cutoff = DateTimeOffset.UtcNow - Ttl;

        var stale = (await db.PendingModalInputs.ToListAsync())
            .Where(p => p.CreatedAt < cutoff)
            .ToList();

        if (stale.Count == 0)
            return;

        db.PendingModalInputs.RemoveRange(stale);
        await db.SaveChangesAsync();
    }
}
