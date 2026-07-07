namespace HoshiBot.Domain.Entities;

public class StfcVeilGroupDiscordInvite
{
    public int Id { get; set; }

    public int VeilGroupId { get; set; }

    public StfcVeilGroup VeilGroup { get; set; } = null!;

    public required string Url { get; set; }
}
