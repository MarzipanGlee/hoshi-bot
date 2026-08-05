namespace HoshiBot.Domain.Entities;

// Unlike legacy (which persists only {ChannelId, MessageId} and re-reads everything else
// live from the Discord message's embed), this stores the real content — enabling
// audit/history that legacy structurally can't have.
public class Announcement
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong ChannelId { get; set; }

    public ulong MessageId { get; set; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public AnnouncementSeverity Severity { get; set; }

    // Which audience this was published for — resolved at publish time from the draft
    // channel it was created in (see AnnouncementDraftService). Audit/reporting
    // only; doesn't affect delivery (already decided by the time this row is created).
    public GuildAudience Audience { get; set; }

    // The role actually pinged at publish time (derived from Severity — see
    // AnnouncementService.ResolveMentionRoleAsync) — stored for audit even though it's
    // derived, since the underlying role mapping could change later.
    public ulong? MentionRoleId { get; set; }

    // Resolved once at publish time, e.g. "Hoshi Sato im Auftrag von LF-Führungsstab".
    public required string Attribution { get; set; }

    public string[] AttachmentUrls { get; set; } = [];

    public ulong TriggeredByDiscordUserId { get; set; }

    public DateTimeOffset SentAt { get; set; }

    // Lets the refresh job skip a no-op editMessage call when the read count hasn't
    // changed since the last run.
    public int LastKnownReadCount { get; set; }

    public ICollection<AnnouncementReadReceipt> ReadReceipts { get; set; } = [];
}
