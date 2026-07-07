namespace HoshiBot.Domain.Entities;

public class ShieldReminderNotification
{
    public int Id { get; set; }

    public int ShieldReminderId { get; set; }

    public ShieldReminder ShieldReminder { get; set; } = null!;

    public NotificationKind Kind { get; set; }

    public ulong ChannelId { get; set; }

    public ulong? MessageId { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
