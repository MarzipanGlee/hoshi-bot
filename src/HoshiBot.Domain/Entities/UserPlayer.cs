namespace HoshiBot.Domain.Entities;

// A user can link multiple players, one marked main. IsMain uniqueness-per-user is
// enforced in application code (unset any prior main before setting a new one), not
// a filtered unique index — simpler, and sufficient for a personal-scale bot.
public class UserPlayer
{
    public int Id { get; set; }

    public ulong DiscordUserId { get; set; }

    public DiscordUser User { get; set; } = null!;

    public int StfcPlayerId { get; set; }

    public StfcPlayer StfcPlayer { get; set; } = null!;

    public bool IsMain { get; set; }
}
