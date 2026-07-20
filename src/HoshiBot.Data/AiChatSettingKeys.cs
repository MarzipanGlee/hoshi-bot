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

    // The Gemini model used for the passive-listening gate pass when a guild hasn't set a GateModel
    // override — the cheap "flash-lite" tier (a fraction of the flash cost) is plenty for a one-word
    // yes/no classification. This is the current flash-lite line, which trails flash's version
    // (flash is 3.5, flash-lite is 3.1); a guild can override per-guild. Since the gate only ever
    // suppresses on a confident NO, a wrong/retired name just makes the gate no-op (falls through to
    // the main model) rather than breaking passive listening.
    public const string DefaultGateModel = "gemini-3.1-flash-lite";

    // The guild's own Google Gemini API key. Stored plaintext for now (see docs/backlog.md
    // "Encrypt per-guild secrets stored in the DB"); a guild with no key set stays silent.
    public const string ApiKey = "ApiKey";

    // Which LLM backend this guild answers with: "gemini" (default) or "ollama". Parsed into
    // AiProvider by AiChatService; unset/unknown falls back to Gemini.
    public const string Provider = "Provider";

    // Optional extra persona / instructions prepended to the built system prompt.
    public const string SystemPrompt = "SystemPrompt";

    // Optional Gemini model override; falls back to GeminiClient.DefaultModel when unset.
    public const string Model = "Model";

    // Optional override for the passive-listening gate model (the small/fast classifier that decides
    // whether a non-addressed message is worth a full answer). Empty falls back to the provider's
    // DefaultGateModel (Gemini: a flash-lite; Ollama: only if Ollama:GateModel is configured). The
    // literal value "off" disables the gate for this guild (back to the main model deciding).
    public const string GateModel = "GateModel";

    // Opt-in live response streaming: when "true", answers are posted as a placeholder / typing
    // indicator and edited in place as the model generates, instead of one message at the end. Unset
    // (default) → off, classic post-once. Stored as the literal string "true".
    public const string StreamResponses = "StreamResponses";

    // Optional complexity-router model. When set, a cheap classifier (this model) decides SIMPLE vs
    // COMPLEX for each answered message: SIMPLE questions are answered by this same model, COMPLEX
    // ones escalate to the main Model. Empty / "off" (default) → routing off, everything uses Model.
    // Motivated by Gemini's per-model request-per-day limits (flash-lite 500/day vs flash 20/day):
    // set this to gemini-3.1-flash-lite so only genuinely complex questions spend the tiny flash
    // quota. Provider-agnostic — an Ollama guild could point it at a smaller local model.
    public const string RouterModel = "RouterModel";

    // Optional model for member-lore background tasks (the DM interviews and note extraction). These
    // are frequent, casual/structured calls that don't need the premium answer model, so they default
    // to the cheap flash-lite tier (the provider's DefaultGateModel) to keep them off Gemini flash's
    // tiny 20/day request cap — the interviews were the biggest hidden consumer of that quota. Empty →
    // DefaultGateModel (falling back to the main model for providers with no gate model, e.g. Ollama).
    public const string MemberLoreModel = "MemberLoreModel";

    // Opt-in: when "true", Hoshi forms and recalls memories — the consolidation job distils notable
    // community events from chat/conversations into GuildMemory rows, and answers get a "was du über
    // die jüngere Geschichte weißt" block. Unset (default) → off. Stored as the literal string "true".
    public const string MemoryEnabled = "MemoryEnabled";

    // Internal (not user-facing): ISO-8601 high-water mark of the newest message the memory
    // consolidation job has already processed for this guild, so each run only distils what's new.
    public const string MemoryWatermark = "MemoryWatermark";

    // The Postgres full-text-search config used to index/search this guild's knowledge content
    // (a regconfig name like "german"/"english"/"simple"). Unset falls back to a value derived
    // from the guild's Discord preferred locale — see FtsLanguage.
    public const string SearchLanguage = "SearchLanguage";
}
