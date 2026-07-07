namespace HoshiBot.Domain.Entities;

public class StfcPlayer
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public int ServerId { get; set; }

    public StfcServer Server { get; set; } = null!;

    public int? AllianceId { get; set; }

    public StfcAlliance? Alliance { get; set; }

    public ICollection<UserPlayer> UserLinks { get; set; } = [];
}
