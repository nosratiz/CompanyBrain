using CompanyBrain.Dashboard.Features.AutoSync.Models;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Stub provider for Notion ingestion.
///
/// <para>
/// Notion requires a dedicated integration token and uses a private REST API
/// (pages, blocks, databases).  Full implementation is deferred until the
/// Notion integration module is available.  This stub returns a clear error
/// so operators know the provider is registered but not yet functional.
/// </para>
/// </summary>
internal sealed class NotionIngestionProvider(
    ILogger<NotionIngestionProvider> logger) : IIngestionProvider
{
    private const string NotImplementedMessage =
        "Notion ingestion is not yet implemented. "
        + "Configure a Notion integration token and implement the Notion REST API client "
        + "before enabling Notion schedules.";

    public SourceType SourceType => SourceType.Notion;

    public Task<IngestionResult> SyncAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Notion sync attempted for schedule {Id} ({Url}) but the provider is not implemented.",
            schedule.Id, schedule.SourceUrl);

        return Task.FromResult(IngestionResult.Failure(NotImplementedMessage));
    }
}
