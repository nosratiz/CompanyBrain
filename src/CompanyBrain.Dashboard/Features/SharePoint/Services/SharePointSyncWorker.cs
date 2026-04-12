using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyBrain.Dashboard.Features.SharePoint.Services;

/// <summary>
/// Background service that performs periodic SharePoint delta synchronization.
/// Runs every 30 minutes (configurable) to sync all enabled folders.
/// </summary>
public sealed class SharePointSyncWorker(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<SharePointDbContext> dbContextFactory,
    SharePointSettingsProvider settingsProvider,
    ILogger<SharePointSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Get initial settings
        var options = await settingsProvider.GetEffectiveOptionsAsync(stoppingToken);
        
        logger.LogInformation("SharePoint Sync Worker started. Interval: {Interval} minutes",
            options.SyncIntervalMinutes);

        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Only sync if properly configured
                if (await settingsProvider.IsConfiguredAsync(stoppingToken))
                {
                    await SyncAllEnabledFoldersAsync(stoppingToken);
                }
                else
                {
                    logger.LogDebug("SharePoint sync skipped - not configured");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during SharePoint sync cycle");
            }

            // Wait for next sync interval (re-read settings in case it changed)
            options = await settingsProvider.GetEffectiveOptionsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes((double)options.SyncIntervalMinutes), stoppingToken);
        }

        logger.LogInformation("SharePoint Sync Worker stopped");
    }

    /// <summary>
    /// Syncs all enabled SharePoint folders.
    /// </summary>
    private async Task SyncAllEnabledFoldersAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var enabledFolders = await db.SyncedFolders
            .Where(f => f.IsEnabled)
            .Select(f => new { f.Id, f.SiteName, f.DriveName, f.LastSyncedAtUtc })
            .ToListAsync(cancellationToken);

        if (enabledFolders.Count == 0)
        {
            logger.LogDebug("No enabled sync folders found");
            return;
        }

        logger.LogInformation("Starting sync cycle for {Count} folders", enabledFolders.Count);

        var successCount = 0;
        var failCount = 0;

        foreach (var folder in enabledFolders)
        {
            try
            {
                logger.LogDebug("Syncing {Site}/{Drive}...", folder.SiteName, folder.DriveName);
                
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var syncService = scope.ServiceProvider.GetRequiredService<SharePointSyncService>();
                await syncService.SyncFolderAsync(folder.Id, cancellationToken);
                successCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                logger.LogError(ex, "Failed to sync folder {Id}: {Site}/{Drive}",
                    folder.Id, folder.SiteName, folder.DriveName);
            }
        }

        logger.LogInformation("Sync cycle complete. Success: {Success}, Failed: {Failed}",
            successCount, failCount);
    }

    /// <summary>
    /// Triggers an immediate sync for a specific folder.
    /// </summary>
    public async Task TriggerSyncAsync(int folderId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Manual sync triggered for folder {FolderId}", folderId);
        
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var syncService = scope.ServiceProvider.GetRequiredService<SharePointSyncService>();
        await syncService.SyncFolderAsync(folderId, cancellationToken);
    }
}
