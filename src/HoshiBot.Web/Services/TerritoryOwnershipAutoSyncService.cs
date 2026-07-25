namespace HoshiBot.Web.Services;

// Refreshes StfcTerritoryOwnership from stfc.pro's live feed automatically — on startup, then hourly —
// so ownership stays current as captures shift it, without an admin uploading a file. Mirrors
// TerritoryServiceAutoSyncService / StfcSystemSyncService. The feed has no version to gate on, so each
// tick does a full fetch+upsert (idempotent — only differing rows change).
public class TerritoryOwnershipAutoSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<TerritoryOwnershipAutoSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

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
            var sync = scope.ServiceProvider.GetRequiredService<StfcTerritoryOwnershipSyncService>();
            var r = await sync.SyncFromSourceAsync();
            logger.LogInformation(
                "Territory ownership auto-sync: {Added} added, {Updated} updated, {Removed} removed ({UnknownTerritory} unknown territory, {UnresolvedTag} unresolved tag skipped).",
                r.Added, r.Updated, r.Removed, r.UnknownTerritory, r.UnresolvedTag);
        }
        catch (Exception ex)
        {
            // Swallow so a transient failure doesn't tear down the timer loop — it retries next tick.
            logger.LogError(ex, "Territory ownership auto-sync failed");
        }
    }
}
