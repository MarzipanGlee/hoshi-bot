namespace HoshiBot.Domain.Entities;

public class NotificationRole
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong DiscordRoleId { get; set; }

    public NotificationRoleKind Kind { get; set; }
}
