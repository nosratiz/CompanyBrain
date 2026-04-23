using CompanyBrain.Dashboard.Features.AutoSync.Models;
using CompanyBrain.Dashboard.Features.Confluence.Services;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Delegates Confluence ingestion to the existing <see cref="ConfluenceSyncWorker"/>,
/// which uses the Confluence REST API (version-based incremental sync).
///
/// <para>
/// The Confluence sync layer already stores page version numbers and only re-downloads
/// pages whose version has increased, so a separate client-side hash is redundant.
/// </para>
///
/// <para>
/// The <see cref="SyncSchedule.SourceUrl"/> is used for logging/identification only;
/// the worker syncs <em>all</em> currently enabled Confluence spaces.
/// </para>
/// </summary>
internal sealed class ConfluenceIngestionProvider(
    ConfluenceSyncWorker confluenceSyncWorker,
    ILogger<ConfluenceIngestionProvider> logger) : IIngestionProvider
{
    public SourceType SourceType => SourceType.Confluence;

    public async Task<IngestionResult> SyncAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "AutoSync delegating to ConfluenceSyncWorker for schedule {Id} ({Url})",
            schedule.Id, schedule.SourceUrl);

        try
        {
            var (success, failed) = await confluenceSyncWorker.TriggerSyncAllAsync(cancellationToken);

            if (failed > 0 && success == 0)
            {
                return IngestionResult.Failure(
                    $"Confluence sync completed with {failed} failed space(s) and 0 successes.");
            }

            logger.LogInformation(
                "Confluence sync finished for schedule {Id}: {Success} succeeded, {Failed} failed",
                schedule.Id, success, failed);

            return IngestionResult.Succeeded(contentHash: null);
        }
        catch (Exception ex)
        {
            return IngestionResult.Failure($"Confluence sync threw an exception: {ex.Message}");
        }
    }
}
