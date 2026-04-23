using CompanyBrain.Dashboard.Features.AutoSync.Models;

namespace CompanyBrain.Dashboard.Features.AutoSync.Providers;

/// <summary>
/// Abstraction for platform-specific ingestion logic.
/// Each implementation knows how to fetch content from one <see cref="SourceType"/>,
/// run the delta hash check, and write changed content into the DeepRoot knowledge store.
/// </summary>
public interface IIngestionProvider
{
    /// <summary>The platform this provider handles.</summary>
    SourceType SourceType { get; }

    /// <summary>
    /// Fetches content from the URL stored in <paramref name="schedule"/>,
    /// compares it against <see cref="SyncSchedule.LastContentHash"/> (delta check),
    /// and persists changed content to the knowledge store.
    /// </summary>
    /// <param name="schedule">The active schedule record including URL, collection, and previous hash.</param>
    /// <param name="cancellationToken">Propagated from the worker; honour promptly.</param>
    /// <returns>
    /// An <see cref="IngestionResult"/> indicating success/failure, whether content changed,
    /// and the new content hash (if applicable).
    /// </returns>
    Task<IngestionResult> SyncAsync(SyncSchedule schedule, CancellationToken cancellationToken);
}
