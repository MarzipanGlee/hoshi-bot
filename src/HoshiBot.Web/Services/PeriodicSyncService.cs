using HoshiBot.Data;

namespace HoshiBot.Web.Services;

// Shared scaffold for the periodic background refreshes (territory ownership, territory
// services, system catalog): run once at startup (optionally gated), then on a fixed
// interval. Each tick resolves TSync from a fresh DI scope; failures are swallowed and
// logged so a transient error doesn't tear down the timer loop — it retries next tick.
public abstract class PeriodicSyncService<TSync>(
    IServiceScopeFactory scopeFactory,
    ILogger logger,
    TimeSpan interval) : BackgroundService
    where TSync : notnull
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TickAsync(startup: true, stoppingToken);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await TickAsync(startup: false, stoppingToken);
    }

    // Whether the startup tick should run at all; later ticks always run.
    protected virtual Task<bool> ShouldRunOnStartupAsync(TSync sync, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    protected abstract Task RunAsync(TSync sync, CancellationToken cancellationToken);

    private async Task TickAsync(bool startup, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sync = scope.ServiceProvider.GetRequiredService<TSync>();
            if (startup && !await ShouldRunOnStartupAsync(sync, cancellationToken))
                return;

            await RunAsync(sync, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{SyncService} periodic run failed", typeof(TSync).Name);
        }
    }
}

// Refreshes StfcTerritoryOwnership from stfc.pro's live feed automatically — on startup, then
// hourly — so ownership stays current as captures shift it, without an admin uploading a file.
// The feed has no version to gate on, so each tick does a full fetch+upsert (idempotent — only
// differing rows change).
public class TerritoryOwnershipAutoSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<TerritoryOwnershipAutoSyncService> logger)
    : PeriodicSyncService<StfcTerritoryOwnershipSyncService>(scopeFactory, logger, TimeSpan.FromHours(1))
{
    protected override async Task RunAsync(StfcTerritoryOwnershipSyncService sync, CancellationToken cancellationToken)
    {
        var r = await sync.SyncFromSourceAsync();
        logger.LogInformation(
            "Territory ownership auto-sync: {Added} added, {Updated} updated, {Removed} removed ({UnknownTerritory} unknown territory, {UnresolvedTag} unresolved tag skipped).",
            r.Added, r.Updated, r.Removed, r.UnknownTerritory, r.UnresolvedTag);
    }
}

// Runs the territory.lol service sync automatically ~twice a day (and once on startup / after a
// deploy), so a new TC season's catalog + per-server slots refresh without an admin clicking the
// manual "Sync now" button. The sync itself meta.json-gates (skips when tcSeason/generatedAt are
// unchanged), so ticks between seasons are cheap no-ops.
public class TerritoryServiceAutoSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<TerritoryServiceAutoSyncService> logger)
    : PeriodicSyncService<TerritoryServiceSyncService>(scopeFactory, logger, TimeSpan.FromHours(12))
{
    protected override async Task RunAsync(TerritoryServiceSyncService sync, CancellationToken cancellationToken)
    {
        var result = await sync.SyncAsync(force: false);
        if (!result.Skipped)
            logger.LogInformation(
                "Territory service auto-sync (season {Season}): {Services} services, {Servers} servers, {Slots} slots.",
                result.Season, result.ServicesUpserted, result.ServersSynced, result.SlotsInserted);
    }
}

// Syncs the StfcSystem catalog from stfc.space — immediately on startup if the table is empty,
// then once a day. The sync service does its own result logging.
public class StfcSystemAutoSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<StfcSystemAutoSyncService> logger)
    : PeriodicSyncService<StfcSystemSyncService>(scopeFactory, logger, TimeSpan.FromDays(1))
{
    protected override Task<bool> ShouldRunOnStartupAsync(StfcSystemSyncService sync, CancellationToken cancellationToken) =>
        sync.IsEmptyAsync(cancellationToken);

    protected override Task RunAsync(StfcSystemSyncService sync, CancellationToken cancellationToken) =>
        sync.SyncAsync(cancellationToken);
}
