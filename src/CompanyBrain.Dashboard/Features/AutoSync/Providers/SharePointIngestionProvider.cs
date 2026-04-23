using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.SharePoint.Services;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Delegates SharePoint ingestion to the existing <see cref="SharePointSyncWorker"/>,
/// which uses Microsoft Graph delta queries for efficient incremental sync.
///
/// <para>
/// Because Graph delta queries already handle change detection at the drive-item level
/// (server-side diffing), this provider does <em>not</em> apply a client-side SHA-256
/// content hash.  <see cref="Models.IngestionResult.ContentHash"/> is returned as
/// <see langword="null"/> and <see cref="Models.IngestionResult.ContentChanged"/> reflects
/// whether at least one folder synced successfully.
/// </para>
///
/// <para>
/// The <see cref="SyncSchedule.SourceUrl"/> is used for logging/identification only;
/// the worker syncs <em>all</em> currently enabled SharePoint folders.  If per-folder
/// scheduling is required, extend this provider to resolve the folder ID from the URL
/// and call <see cref="SharePointSyncWorker.TriggerSyncAsync"/>.
/// </para>
/// </summary>
internal sealed class SharePointIngestionProvider(
    SharePointSyncWorker sharePointSyncWorker,
    ILogger<SharePointIngestionProvider> logger) : IIngestionProvider
{
    public SourceType SourceType => SourceType.SharePoint;

    public async Task<IngestionResult> SyncAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "AutoSync delegating to SharePointSyncWorker for schedule {Id} ({Url})",
            schedule.Id, schedule.SourceUrl);

        try
        {
            var (success, failed) = await sharePointSyncWorker.TriggerSyncAllAsync(cancellationToken);

            if (failed > 0 && success == 0)
            {
                return IngestionResult.Failure(
                    $"SharePoint sync completed with {failed} failed folder(s) and 0 successes.");
            }

            // Graph delta queries handle their own change detection — return Succeeded so
            // the worker updates LastSyncUtc even when no files actually changed this run.
            logger.LogInformation(
                "SharePoint sync finished for schedule {Id}: {Success} succeeded, {Failed} failed",
                schedule.Id, success, failed);

            return IngestionResult.Succeeded(contentHash: null);
        }
        catch (Exception ex)
        {
            return IngestionResult.Failure($"SharePoint sync threw an exception: {ex.Message}");
        }
    }
}
