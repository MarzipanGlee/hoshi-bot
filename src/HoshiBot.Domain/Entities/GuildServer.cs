namespace HoshiBot.Domain.Entities;

// Which shared STFC servers this guild tracks (e.g. a whole-server community Discord,
// not tied to any single alliance). Independent of GuildAlliance/GuildVeilGroup — a
// guild can link any combination of these.
public class GuildServer
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public int StfcServerId { get; set; }

    public StfcServer StfcServer { get; set; } = null!;
}
