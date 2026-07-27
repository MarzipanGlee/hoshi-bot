namespace HoshiBot.Domain.Entities;

// Global, not guild-scoped — a person's linked STFC player(s) are a fact about
// them, not about one guild. Per-guild membership facts live on GuildMember.
public class DiscordUser
{
    public ulong DiscordUserId { get; set; }

    // Explicit bot-language preference (ISO 639-1 code, see Languages.ToCode), set via
    // /me. Null = automatic: resolve from the Discord locale (live interaction locale,
    // else DiscordLocale below) — see LanguagePolicy.ForUser.
    public string? Language { get; set; }

    // The user's Discord client locale as last seen on any interaction
    // (Interaction.UserLocale), recorded by UserLocaleSyncHandler. Lets DMs and jobs —
    // which have no interaction in hand — still resolve the user's automatic language.
    public string? DiscordLocale { get; set; }

    public ICollection<UserPlayer> PlayerLinks { get; set; } = [];

    public ICollection<GuildMember> GuildMemberships { get; set; } = [];
}
