namespace HoshiBot.Domain.Entities;

// A user's linked players — a flat set, with no globally "main" one: which player represents
// the person is a per-guild fact and lives on GuildMember.PrimaryStfcPlayerId (someone can play
// a different character in each community). Two Discord accounts sharing a player is what makes
// them the same person; see PlayerLinkService.GetAccountGroupAsync.
public class UserPlayer
{
    public int Id { get; set; }

    public ulong DiscordUserId { get; set; }

    public DiscordUser User { get; set; } = null!;

    public int StfcPlayerId { get; set; }

    public StfcPlayer StfcPlayer { get; set; } = null!;
}
