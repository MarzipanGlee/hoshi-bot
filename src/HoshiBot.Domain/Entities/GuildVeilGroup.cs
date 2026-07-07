namespace HoshiBot.Domain.Entities;

// Which shared STFC veil groups this guild tracks (e.g. a community Discord spanning
// every server in a veil group). Independent of GuildAlliance/GuildServer — a guild can
// link any combination of these.
public class GuildVeilGroup
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public int StfcVeilGroupId { get; set; }

    public StfcVeilGroup StfcVeilGroup { get; set; } = null!;
}
