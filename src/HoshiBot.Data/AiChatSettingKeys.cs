namespace HoshiBot.Data;

// Key strings for the AI-chat feature's per-audience behavioral settings in
// GuildFeatureSettingsService (text values under GuildFeature.AiChat at the per-audience scope —
// each audience/alliance tab has its own). The guild-wide backend scalars (provider, API key,
// models, embeddings) are NOT here — they live in AiBackendSettingKeys under GuildFeature.AiBackend.
// The channel lists are also not here — listen channels are GuildFeatureChannel rows under
// GuildFeature.AiChat, knowledge channels under GuildFeature.AiChatKnowledge.
public static class AiChatSettingKeys
{
    // Optional extra persona / instructions prepended to the built system prompt. Per-audience:
    // each alliance/community can give Hoshi its own house voice / extra lore.
    public const string SystemPrompt = "SystemPrompt";

    // Opt-in live response streaming: when "true", answers are posted as a placeholder / typing
    // indicator and edited in place as the model generates, instead of one message at the end. Unset
    // (default) → off, classic post-once. Stored as the literal string "true".
    public const string StreamResponses = "StreamResponses";

    // Opt-in: when "true", Hoshi forms and recalls memories — the consolidation job distils notable
    // community events from chat/conversations into GuildMemory rows, and answers get a "was du über
    // die jüngere Geschichte weißt" block. Unset (default) → off. Stored as the literal string "true".
    // Per-audience toggle; the guild-wide memory jobs run if it's enabled under ANY audience (the
    // GuildMemory store itself is guild-wide).
    public const string MemoryEnabled = "MemoryEnabled";

    // Internal (not user-facing): ISO-8601 high-water mark of the newest message the memory
    // consolidation job has already processed for this guild, so each run only distils what's new.
    // Deliberately stays GUILD-WIDE (None scope) — the consolidation job is guild-wide, so this is
    // one shared cursor, not a per-audience value.
    public const string MemoryWatermark = "MemoryWatermark";

    // The Postgres full-text-search config used to index/search this guild's knowledge content
    // (a regconfig name like "german"/"english"/"simple"). Unset falls back to a value derived
    // from the guild's Discord preferred locale — see FtsLanguage. Per-audience, since knowledge
    // channels are per-audience.
    public const string SearchLanguage = "SearchLanguage";
}
