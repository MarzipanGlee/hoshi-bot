namespace HoshiBot.Data;

// Key strings for the guild-wide AI backend feature's settings in GuildFeatureSettingsService
// (stored under GuildFeature.AiBackend at the Guild/null scope — one AI account per guild). These
// used to live under GuildFeature.AiChat at the None scope; they were split out so the per-audience
// AiChat feature no longer carries guild-wide credentials/models. Every AI-powered feature (AiChat,
// MemberLore, AnnouncementForwarder) reads its provider/key/models from here.
public static class AiBackendSettingKeys
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

    // The guild's own Google Gemini API key (encrypted at rest via SettingSecretProtector). A guild
    // with no key set stays silent when the provider is Gemini; Ollama needs none.
    public const string ApiKey = "ApiKey";

    // Which LLM backend this guild answers with: "gemini" (default) or "ollama". Parsed into
    // AiProvider by AiChatService; unset/unknown falls back to Gemini.
    public const string Provider = "Provider";

    // Optional Gemini model override; falls back to GeminiClient.DefaultModel when unset.
    public const string Model = "Model";

    // Optional override for the passive-listening gate model (the small/fast classifier that decides
    // whether a non-addressed message is worth a full answer). Empty falls back to the provider's
    // DefaultGateModel (Gemini: a flash-lite; Ollama: only if Ollama:GateModel is configured). The
    // literal value "off" disables the gate for this guild (back to the main model deciding).
    public const string GateModel = "GateModel";

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

    // Which embedding backend powers semantic search (the vector leg of hybrid knowledge retrieval)
    // and episodic/member memory recall — independent of Provider above (chat and embeddings can
    // use different backends). Stored value is the literal effective identifier:
    //   - unset / "ollama" / any unrecognized value -> Ollama, the shared local server's
    //     Ollama:EmbeddingModel deployment config (today's behavior — the only default that never
    //     changes an existing guild's behavior or cost).
    //   - "gemini-embedding-001" -> Google's gemini-embedding-001 (text-only).
    //   - "gemini-embedding-2" -> Google's gemini-embedding-2 (multimodal-capable at the API level;
    //     only text input is exercised today — see docs/backlog.md's image/vision backlog item).
    // Both Gemini options reuse this guild's existing ApiKey (the same key already configured for
    // chat) and are truncated to a fixed 768-dim output (AiChatEmbeddingService.Dimensions) to
    // match the vector(768) column — no schema migration involved in switching.
    public const string EmbeddingProvider = "EmbeddingProvider";
}
