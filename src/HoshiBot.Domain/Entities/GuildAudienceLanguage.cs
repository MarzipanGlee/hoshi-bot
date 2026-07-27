namespace HoshiBot.Domain.Entities;

// Explicit bot-language override for one of a guild's non-Alliance audiences (Server /
// VeilGroup / Community) — a row exists only when an admin explicitly set one; absence
// means "inherit the guild language" (LanguagePolicy.ForAudience). The Alliance audience
// is covered per-alliance (GuildAlliance.Language) and the Guild pseudo-audience by
// GuildSettings.Language, so neither ever gets a row here. Deliberately NOT a
// GuildFeatureSettingText row: language is feature-agnostic (wrong altitude there).
public class GuildAudienceLanguage
{
    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildAudience Audience { get; set; }

    // ISO 639-1 code, see Languages.ToCode.
    public required string Language { get; set; }
}
