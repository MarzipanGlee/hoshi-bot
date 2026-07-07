namespace HoshiBot.Domain.Entities;

// A reported raid (or, in the future, station-move) alert for one target commander.
public class Alert
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public AlertType Type { get; set; }

    public ulong TargetDiscordUserId { get; set; }

    public int? StfcSystemId { get; set; }

    public StfcSystem? StfcSystem { get; set; }

    public string? Detail { get; set; }

    // Raid-specific fields — meaningless for a future StationMove alert, same spirit as
    // StfcSystemId already being raid-leaning despite living on the shared table.
    public string? Attacker { get; set; }

    public RaidServerLocation ServerLocation { get; set; }

    // True when the reporter targeted themselves — a safe, free signal for "just trying
    // the flow out," not a real raid. Still goes through the full report/terminate
    // lifecycle, but excluded from any real raid history/reporting.
    public bool IsTest { get; set; }

    public ulong TriggeredByDiscordUserId { get; set; }

    public DateTimeOffset TriggeredAt { get; set; }

    public DateTimeOffset? TerminatedAt { get; set; }

    public ulong? TerminatedByDiscordUserId { get; set; }

    public ICollection<AlertNotification> Notifications { get; set; } = [];
}
