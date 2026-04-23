using CompanyBrain.Dashboard.Features.Confluence.Data;
using CompanyBrain.Dashboard.Features.Confluence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompanyBrain.Dashboard.Features.Confluence.Services;

/// <summary>
/// Background service that periodically syncs all enabled Confluence spaces.
/// Resolves ConfluenceSyncService per-scope to satisfy scoped vs singleton lifetime rules.
/// </summary>
public sealed class ConfluenceSyncWorker(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<ConfluenceDbContext> dbContextFactory,
    ConfluenceSettingsProvider settingsProvider,
    IOptions<ConfluenceSyncOptions> options,
    ILogger<ConfluenceSyncWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Confluence sync worker started. Interval: {Interval} min",
            options.Value.SyncIntervalMinutes);

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await settingsProvider.IsConfiguredAsync(stoppingToken))
                    await RunSyncCycleAsync(stoppingToken);
                else
                    logger.LogDebug("Confluence sync skipped — not configured");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Confluence sync cycle");
            }

            await Task.Delay(TimeSpan.FromMinutes(options.Value.SyncIntervalMinutes), stoppingToken);
        }

        logger.LogInformation("Confluence sync worker stopped");
    }

    /// <summary>
    /// Triggers an immediate sync of all enabled spaces. Returns (successCount, failedCount).
    /// </summary>
    public async Task<(int Success, int Failed)> TriggerSyncAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            logger.LogWarning("Confluence sync already in progress, skipping manual trigger");
            return (0, 0);
        }

        try
        {
            return await RunSyncCycleAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Triggers sync for a single space by its database ID.
    /// </summary>
    public async Task TriggerSyncAsync(int syncedSpaceId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Manual sync triggered for Confluence space {Id}", syncedSpaceId);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ConfluenceSyncService>();
        await syncService.SyncSpaceAsync(syncedSpaceId, cancellationToken);
    }

    private async Task<(int Success, int Failed)> RunSyncCycleAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var enabledIds = await db.SyncedSpaces
            .Where(s => s.IsEnabled)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (enabledIds.Count == 0)
        {
            logger.LogDebug("No enabled Confluence spaces to sync");
            return (0, 0);
        }

        logger.LogInformation("Starting Confluence sync cycle for {Count} space(s)", enabledIds.Count);

        var success = 0;
        var failed = 0;

        foreach (var id in enabledIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ConfluenceSyncService>();
                await syncService.SyncSpaceAsync(id, cancellationToken);
                success++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Sync failed for Confluence space {Id}", id);
            }
        }

        logger.LogInformation("Confluence sync cycle complete: {Success} succeeded, {Failed} failed", success, failed);
        return (success, failed);
    }
}
