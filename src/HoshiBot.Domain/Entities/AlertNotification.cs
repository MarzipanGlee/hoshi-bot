namespace HoshiBot.Domain.Entities;

// One row per channel a public notification was fanned out to, plus one User-kind
// row tracking the DM sent to the alert's target.
public class AlertNotification
{
    public int Id { get; set; }

    public int AlertId { get; set; }

    public Alert Alert { get; set; } = null!;

    public NotificationKind Kind { get; set; }

    public ulong ChannelId { get; set; }

    public ulong? MessageId { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
