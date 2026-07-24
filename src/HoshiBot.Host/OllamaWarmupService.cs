using System.Net.Http.Json;
using HoshiBot.Data;
using HoshiBot.Discord.AiChat;
using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HoshiBot.Host;

// Preloads the Ollama models the bot actually uses — the deployment-wide embedding model plus each
// in-use Ollama chat model — once at startup, so the first AI-chat reply / knowledge search after a
// (re)start doesn't pay the cold-load penalty. With the ollama service's OLLAMA_KEEP_ALIVE=-1 a
// single load keeps them resident, so this runs once. Best-effort: never throws, never blocks the
// rest of startup (BackgroundService runs after the host is up).
public class OllamaWarmupService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OllamaWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var models = await ResolveModelsToWarmAsync(stoppingToken);
            if (models.Count == 0)
                return;

            var http = httpClientFactory.CreateClient(nameof(OllamaClient));
            if (!await WaitForOllamaAsync(http, stoppingToken))
            {
                logger.LogWarning("Ollama warmup skipped: server not reachable at {BaseUrl}", http.BaseAddress);
                return;
            }

            var warmed = new List<string>();
            var failed = new List<string>();
            foreach (var (model, isEmbedding) in models)
                (await WarmModelAsync(http, model, isEmbedding, stoppingToken) ? warmed : failed).Add(model);

            logger.LogInformation(
                "Ollama warmup: loaded [{Warmed}]{Failed}",
                string.Join(", ", warmed),
                failed.Count > 0 ? $"; failed [{string.Join(", ", failed)}]" : "");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Ollama warmup failed");
        }
    }

    // Distinct (model, isEmbedding) targets: the configured embedding model (always, if set) plus the
    // chat model of every guild whose AI-chat provider is Ollama (its override or the default).
    private async Task<List<(string Model, bool IsEmbedding)>> ResolveModelsToWarmAsync(CancellationToken cancellationToken)
    {
        var models = new Dictionary<string, bool>(); // model -> isEmbedding

        if (configuration["Ollama:EmbeddingModel"] is { Length: > 0 } embeddingModel)
            models[embeddingModel] = true;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HoshiBotDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<GuildFeatureSettingsService>();

        var guildIds = await db.GuildEnabledFeatures
            .Where(f => f.Feature == GuildFeature.AiChat)
            .Select(f => f.GuildId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var defaultChatModel = configuration["Ollama:DefaultModel"];
        foreach (var guildId in guildIds)
        {
            var provider = await settings.GetTextAsync(guildId, GuildFeature.AiBackend, GuildAudience.Guild, null, AiBackendSettingKeys.Provider);
            if (!string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
                continue;

            var model = await settings.GetTextAsync(guildId, GuildFeature.AiBackend, GuildAudience.Guild, null, AiBackendSettingKeys.Model);
            model = string.IsNullOrWhiteSpace(model) ? defaultChatModel : model.Trim();
            if (!string.IsNullOrWhiteSpace(model))
                models.TryAdd(model, false);
        }

        return models.Select(kv => (kv.Key, kv.Value)).ToList();
    }

    // Ollama may still be starting alongside the bot — poll /api/version briefly before warming.
    private static async Task<bool> WaitForOllamaAsync(HttpClient http, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var response = await http.GetAsync("api/version", cancellationToken);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // not up yet
            }
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
        return false;
    }

    // Loads a model into memory without generating: /api/generate with no prompt (chat models) or a
    // trivial /api/embed (embedding models). keep_alive is governed by the server env (-1).
    private async Task<bool> WarmModelAsync(HttpClient http, string model, bool isEmbedding, CancellationToken cancellationToken)
    {
        try
        {
            using var response = isEmbedding
                ? await http.PostAsJsonAsync("api/embed", new { model, input = "warmup" }, cancellationToken)
                : await http.PostAsJsonAsync("api/generate", new { model }, cancellationToken);
            if (response.IsSuccessStatusCode)
                return true;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Ollama warmup for {Model} failed (status {Status}): {Error}", model, (int)response.StatusCode, body);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Ollama warmup for {Model} failed", model);
            return false;
        }
    }
}
