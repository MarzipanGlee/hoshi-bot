namespace HoshiBot.Domain.Entities;

// What a guild may pick in the AI Provider editor, and what it gets when it picks nothing.
//
// Transcribed from Google's deprecation page
// (https://ai.google.dev/gemini-api/docs/deprecations), which is the only place that says which
// models are still alive. Two rules decide what appears here:
//
//   1. No PREVIEW models. They are the ones with short, already-announced shutdowns — every preview
//      on that page today has a retirement date, several within months.
//   2. No model with an announced shutdown date, even a distant one. An announced shutdown is
//      Google saying "this is going away"; offering it as a fresh choice only creates work later.
//
// A stored model that has since left this list still WORKS and is still shown in the editor as the
// current value — dropping it from the options must never silently rewrite a guild's configuration.
// The editor marks it, so an admin can see why it is not among the offered choices.
//
// Keeping the release date turns "the latest" into something checkable rather than asserted: the
// defaults below are derived from it, so refreshing this list moves them automatically.
public sealed record GeminiModel(string Id, DateOnly Released);

public static class GeminiModels
{
    // Full-size answer models. gemini-2.5-* stay because they carry no shutdown date, even though
    // the 3.x line supersedes them — a guild on a 2.5 model is not being nudged off it by us.
    public static readonly IReadOnlyList<GeminiModel> Chat =
    [
        new("gemini-3.6-flash", new DateOnly(2026, 7, 21)),
        new("gemini-3.5-flash", new DateOnly(2026, 5, 19)),
        new("gemini-2.5-pro", new DateOnly(2025, 6, 17)),
        new("gemini-2.5-flash", new DateOnly(2025, 6, 17)),
    ];

    // The cheap tier the gate, router and member-lore passes run on. Gemini's per-model daily request
    // caps are the whole reason these settings exist — flash-lite allows far more requests per day
    // than flash, so the small flash quota is spent only on genuinely hard questions.
    //
    // Note gemini-3.1-flash-lite is deliberately ABSENT: it was the default until this list existed,
    // and Google has since announced its shutdown (7 May 2027, replaced by gemini-3.5-flash-lite).
    public static readonly IReadOnlyList<GeminiModel> Light =
    [
        new("gemini-3.5-flash-lite", new DateOnly(2026, 7, 21)),
        new("gemini-2.5-flash-lite", new DateOnly(2025, 7, 22)),
    ];

    // gemini-embedding-001 is absent for the same reason: shutdown announced for 14 May 2028.
    // Guilds already on it keep working and keep seeing it selected.
    public static readonly IReadOnlyList<GeminiModel> Embedding =
    [
        new("gemini-embedding-2", new DateOnly(2026, 4, 22)),
    ];

    // Latest by release date, not by list order — so adding a model in the wrong place cannot
    // silently change what every unconfigured guild uses.
    public static string Latest(IReadOnlyList<GeminiModel> models) =>
        models.MaxBy(m => m.Released)!.Id;

    public static string DefaultChat => Latest(Chat);

    public static string DefaultLight => Latest(Light);

    public static string DefaultEmbedding => Latest(Embedding);

    // True when a guild's stored value is not one of the choices above — either retired since it was
    // set, or a name typed by hand. The editor keeps showing it rather than resetting it.
    public static bool IsOffered(string? modelId, IReadOnlyList<GeminiModel> models) =>
        !string.IsNullOrWhiteSpace(modelId) && models.Any(m => m.Id == modelId);
}
