namespace HoshiBot.Data;

// Key strings for the AI-chat feature's scalar settings in GuildFeatureSettingsService (text
// values at the guild-wide GuildFeature.AiChat / Server scope). The channel lists are NOT here —
// listen channels are GuildFeatureChannel rows under GuildFeature.AiChat, knowledge channels
// under GuildFeature.AiChatKnowledge.
public static class AiChatSettingKeys
{
    // The Gemini model used when a guild hasn't set the Model override. Lives here (not on the
    // Discord-side GeminiClient) so the Web editor can show it as the placeholder without a
    // reference to HoshiBot.Discord. Note: plain "gemini-2.5-flash" is no longer served to new
    // API keys ("no longer available to new users") — gemini-3.5-flash is the current stable
    // flash model. A guild can still override this per-guild in the editor.
    public const string DefaultModel = "gemini-3.5-flash";

    // The guild's own Google Gemini API key. Stored plaintext for now (see docs/backlog.md
    // "Encrypt per-guild secrets stored in the DB"); a guild with no key set stays silent.
    public const string ApiKey = "ApiKey";

    // Optional extra persona / instructions prepended to the built system prompt.
    public const string SystemPrompt = "SystemPrompt";

    // Optional Gemini model override; falls back to GeminiClient.DefaultModel when unset.
    public const string Model = "Model";

    // The Postgres full-text-search config used to index/search this guild's knowledge content
    // (a regconfig name like "german"/"english"/"simple"). Unset falls back to a value derived
    // from the guild's Discord preferred locale — see FtsLanguage.
    public const string SearchLanguage = "SearchLanguage";
}
