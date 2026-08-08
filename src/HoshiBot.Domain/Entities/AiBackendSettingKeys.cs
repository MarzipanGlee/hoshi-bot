namespace HoshiBot.Domain.Entities;

// Key strings for the guild-wide AI backend feature's settings in GuildFeatureSettingsService
// (stored under GuildFeature.AiBackend at the Guild/null scope — one AI account per guild). These
// used to live under GuildFeature.AiChat at the None scope; they were split out so the per-audience
// AiChat feature no longer carries guild-wide credentials/models. Every AI-powered feature (AiChat,
// MemberLore, AnnouncementForwarder) reads its provider/key/models from here.
public static class AiBackendSettingKeys
{
    // The Gemini model used when a guild hasn't set the Model override. Lives here (not on the
    // Discord-side GeminiClient) so the Web editor can show it without referencing HoshiBot.Discord.
    //
    // Derived from GeminiModels rather than pinned: hard-coding it is how the gate default came to
    // point at gemini-3.1-flash-lite months after Google announced its shutdown.
    public static string DefaultModel => GeminiModels.DefaultChat;

    // The model for the passive-listening gate pass when a guild hasn't overridden it — the cheap
    // flash-lite tier is plenty for a one-word yes/no classification, and its far larger daily
    // request cap is the point. Also the default for the router and member-lore passes.
    //
    // Since the gate only ever suppresses on a confident NO, a retired name degrades to a no-op
    // (everything falls through to the main model) rather than breaking passive listening — which is
    // exactly why the stale default went unnoticed.
    public static string DefaultGateModel => GeminiModels.DefaultLight;

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
    //   - "gemini-embedding-001" -> Google's gemini-embedding-001 (text-only). Shutdown announced,
    //     so no longer offered as a new choice; a guild already on it keeps it.
    //   - "gemini-embedding-2" -> Google's gemini-embedding-2 (multimodal-capable at the API level;
    //     only text input is exercised today — see ImageEmbeddingProvider below).
    // Both Gemini options reuse this guild's existing ApiKey (the same key already configured for
    // chat) and are truncated to a fixed 768-dim output (AiChatEmbeddingService.Dimensions) to
    // match the vector(768) column — no schema migration involved in switching.
    public const string EmbeddingProvider = "EmbeddingProvider";

    // Which backend will embed IMAGES once Hoshi indexes them — separate from EmbeddingProvider
    // because text and images need not share one: a guild can keep cheap local text embeddings and
    // still pay Gemini for the handful of images worth searching.
    //
    // Nothing reads this yet. It exists so the choice is recorded before the indexing lands, and the
    // editor says so plainly rather than letting an admin set it and wait — the same mistake the
    // alliance channel pickers made. Unset means off, which is also what image indexing does today.
    public const string ImageEmbeddingProvider = "ImageEmbeddingProvider";

    // The "no image embeddings" value, and the default.
    public const string ImageEmbeddingOff = "off";
}
