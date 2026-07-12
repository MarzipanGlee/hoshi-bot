namespace HoshiBot.Domain.Entities;

// One row per guild pinged for a StfcNewsPost, so every guild's own copy of the admin-channel
// message can be edited in place whenever shared state changes (submission, a new
// confirmation, resolution) — the confirmation quorum itself is global across all guilds, not
// per-guild, so every row for the same post is kept in sync showing identical content.
public class StfcNewsPostGuildMessage
{
    public int Id { get; set; }

    public int StfcNewsPostId { get; set; }

    public StfcNewsPost StfcNewsPost { get; set; } = null!;

    public ulong GuildId { get; set; }

    public ulong ChannelId { get; set; }

    public ulong MessageId { get; set; }

    // Snapshot, taken when this guild was pinged, of how many members hold this guild's
    // admin/command-staff role(s) — a proxy for "how many people could plausibly confirm."
    // Stored per-guild so StfcNewsPost.RequiredConfirmations can be computed once from the
    // sum across all guilds without re-fetching guild membership later.
    public int EligibleMemberCount { get; set; }
}
