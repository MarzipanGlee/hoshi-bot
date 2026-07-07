namespace HoshiBot.Domain.Entities;

// Purely the guild-specific Discord role-ID mappings for a shared alliance (e.g.
// "which role marks our officers") — no diplomacy status here, that's
// StfcAllianceDiplomacy, an in-game fact independent of any guild.
public class GuildAlliance
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public int StfcAllianceId { get; set; }

    public StfcAlliance StfcAlliance { get; set; } = null!;

    public ulong? MemberRoleId { get; set; }

    public ulong? OfficerRoleId { get; set; }

    public ulong? DiplomatRoleId { get; set; }
}
