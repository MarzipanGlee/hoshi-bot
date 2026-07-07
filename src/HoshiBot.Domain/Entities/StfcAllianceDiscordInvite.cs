namespace HoshiBot.Domain.Entities;

public class StfcAllianceDiscordInvite
{
    public int Id { get; set; }

    public int AllianceId { get; set; }

    public StfcAlliance Alliance { get; set; } = null!;

    public required string Url { get; set; }
}
