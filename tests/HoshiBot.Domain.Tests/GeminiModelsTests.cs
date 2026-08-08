using HoshiBot.Domain.Entities;

namespace HoshiBot.Domain.Tests;

public class GeminiModelsTests
{
    // The reason this catalog exists. The gate default was pinned to gemini-3.1-flash-lite in a
    // const, Google announced its shutdown, and nothing noticed — a wrong gate model degrades to a
    // no-op rather than an error, so it would have gone stale indefinitely. Defaults are derived
    // from the list now, so refreshing the list moves them.
    [Fact]
    public void Defaults_are_the_newest_model_of_their_kind()
    {
        Assert.Equal(GeminiModels.Chat.MaxBy(m => m.Released)!.Id, GeminiModels.DefaultChat);
        Assert.Equal(GeminiModels.Light.MaxBy(m => m.Released)!.Id, GeminiModels.DefaultLight);
        Assert.Equal(GeminiModels.Embedding.MaxBy(m => m.Released)!.Id, GeminiModels.DefaultEmbedding);
    }

    // Latest is by DATE, not list order — otherwise adding a model in the wrong place silently
    // changes what every unconfigured guild uses.
    [Fact]
    public void Latest_ignores_list_order()
    {
        IReadOnlyList<GeminiModel> models =
        [
            new("newest", new DateOnly(2026, 7, 1)),
            new("oldest", new DateOnly(2024, 1, 1)),
        ];

        Assert.Equal("newest", GeminiModels.Latest(models));
    }

    // The retired names these lists were built to exclude. Each was a live default or option before
    // Google announced its shutdown; a test naming them means re-adding one is a decision, not a
    // slip.
    [Theory]
    [InlineData("gemini-3.1-flash-lite")]   // shutdown 2027-05-07, was the gate default
    [InlineData("gemini-embedding-001")]    // shutdown 2028-05-14
    [InlineData("gemini-2.0-flash")]        // shutdown 2026-06-01
    public void Models_with_an_announced_shutdown_are_not_offered(string modelId)
    {
        var offered = GeminiModels.Chat.Concat(GeminiModels.Light).Concat(GeminiModels.Embedding);
        Assert.DoesNotContain(modelId, offered.Select(m => m.Id));
    }

    // Preview models all carry short shutdowns, so none belongs in a list of things to pick.
    [Fact]
    public void No_preview_models_are_offered()
    {
        var offered = GeminiModels.Chat.Concat(GeminiModels.Light).Concat(GeminiModels.Embedding);
        Assert.DoesNotContain(offered, m => m.Id.Contains("preview", StringComparison.OrdinalIgnoreCase));
    }

    // A guild's stored model must survive leaving the list: the editor shows it as the current value
    // rather than resetting to the default, which would silently rewrite their configuration.
    [Fact]
    public void A_retired_stored_model_is_reported_as_not_offered()
    {
        Assert.False(GeminiModels.IsOffered("gemini-3.1-flash-lite", GeminiModels.Light));
        Assert.True(GeminiModels.IsOffered(GeminiModels.DefaultLight, GeminiModels.Light));
        Assert.False(GeminiModels.IsOffered(null, GeminiModels.Light));
    }
}
