namespace HoshiBot.Domain.Entities;

// Settings that belong to one of a guild's non-Alliance audiences (Server / VeilGroup / Community)
// rather than to any feature — the same altitude GuildAlliance sits at for the Alliance audience,
// and GuildSettings for the guild as a whole.
//
// A row exists only once an admin sets something; absence means "inherit". Deliberately NOT
// GuildFeatureSetting rows: neither the language a scope reads in nor the category its channels are
// created under has anything to do with a particular feature, and putting them there would make
// every feature look like it owned them.
//
// Was GuildAudienceLanguage until the channel category needed the same home. One row per audience
// with a column per setting beats a table per setting.
public class GuildAudienceSettings
{
    public ulong GuildId { get; set; }

    public DiscordGuild Guild { get; set; } = null!;

    public GuildAudience Audience { get; set; }

    // ISO 639-1 code, see Languages.ToCode. Null = inherit the guild language
    // (LanguagePolicy.ForAudience). The Alliance audience is covered per-alliance
    // (GuildAlliance.Language) and the Guild pseudo-audience by GuildSettings.Language, so neither
    // ever gets a row here.
    public string? Language { get; set; }

    // Where this audience's channels are created when a picker offers to make one. Null = the
    // server root, which is what Discord does with no parent. Mirrors
    // GuildAlliance.DefaultChannelCategoryId, which does the same job for the Alliance audience.
    public ulong? DefaultChannelCategoryId { get; set; }
}
