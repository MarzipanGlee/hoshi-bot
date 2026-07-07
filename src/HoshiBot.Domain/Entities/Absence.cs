namespace HoshiBot.Domain.Entities;

// NotificationRoleSyncJob depends on this (own-alliance members on a suppressed
// absence lose the notification role for its duration) — every query other than a
// direct by-ID lookup must filter Status == Confirmed so Draft rows never leak in.
public class Absence
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public ulong DiscordUserId { get; set; }

    public GuildMember GuildMember { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string? Reason { get; set; }

    public bool SuppressNotifications { get; set; }

    public ulong CreatedByDiscordUserId { get; set; }

    public AbsenceVisibility Visibility { get; set; } = AbsenceVisibility.Public;

    // Draft rows hold a create/edit flow's modal input until confirmed (or cancelled/
    // swept away) — see AbsenceStatus and AbsenceService's ConfirmDraftAsync/CancelDraftAsync.
    public AbsenceStatus Status { get; set; } = AbsenceStatus.Confirmed;

    public DateTimeOffset CreatedAt { get; set; }

    // Set only on a Draft row created by the edit flow: the row it would replace on
    // confirm. Null for create-flow drafts and all regular Confirmed rows.
    public int? EditsAbsenceId { get; set; }

    public Absence? EditsAbsence { get; set; }
}
