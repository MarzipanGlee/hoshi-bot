namespace HoshiBot.Web.Services;

// Runs the territory.lol service sync automatically ~twice a day (and once on startup / after a
// deploy), so a new TC season's catalog + per-server slots refresh without an admin clicking the
// manual "Sync now" button. The sync itself meta.json-gates (skips when tcSeason/generatedAt are
// unchanged), so ticks between seasons are cheap no-ops. Mirrors StfcSystemSyncService's shape.
public class TerritoryServiceAutoSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<TerritoryServiceAutoSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync();

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync();
    }

    private async Task RunOnceAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<TerritoryServiceSyncService>();
            var result = await sync.SyncAsync(force: false);
            if (!result.Skipped)
                logger.LogInformation(
                    "Territory service auto-sync (season {Season}): {Services} services, {Servers} servers, {Slots} slots.",
                    result.Season, result.ServicesUpserted, result.ServersSynced, result.SlotsInserted);
        }
        catch (Exception ex)
        {
            // Swallow so a transient failure doesn't tear down the timer loop — it retries next tick.
            logger.LogError(ex, "Territory service auto-sync failed");
        }
    }
}
