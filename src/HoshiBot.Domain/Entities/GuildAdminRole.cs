namespace HoshiBot.Domain.Entities;

public class GuildAdminRole
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public ulong DiscordRoleId { get; set; }
}
