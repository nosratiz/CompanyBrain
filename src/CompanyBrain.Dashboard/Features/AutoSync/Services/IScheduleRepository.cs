using CompanyBrain.Dashboard.Features.AutoSync.Models;

namespace CompanyBrain.Dashboard.Features.AutoSync.Services;

/// <summary>
/// Data-access contract for <see cref="SyncSchedule"/> persistence.
/// Extracted as an interface to allow unit-testing of <see cref="SovereignSyncWorker"/>
/// without a live database.
/// </summary>
public interface IScheduleRepository
{
    /// <summary>Returns all active schedules (including back-off entries).</summary>
    Task<IReadOnlyList<SyncSchedule>> GetActiveSchedulesAsync(CancellationToken cancellationToken);

    /// <summary>Records a successful sync run and stores the updated content hash.</summary>
    Task UpdateAfterSuccessAsync(int scheduleId, string? contentHash, CancellationToken cancellationToken);

    /// <summary>Records a failure, increments the failure counter, and sets back-off.</summary>
    Task UpdateAfterFailureAsync(int scheduleId, string errorMessage, CancellationToken cancellationToken);
}
