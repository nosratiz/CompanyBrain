namespace CompanyBrain.Dashboard.Features.SharePoint.Models;

/// <summary>
/// Represents a conflict detected during SharePoint sync.
/// </summary>
public sealed record SyncConflict(
    int Id,
    string LocalPath,
    string RemotePath,
    string DriveId,
    string ItemId,
    DateTimeOffset LocalLastModified,
    DateTimeOffset RemoteLastModified,
    ConflictResolutionStatus Status,
    DateTimeOffset DetectedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

/// <summary>
/// Resolution status for a sync conflict.
/// </summary>
public enum ConflictResolutionStatus
{
    Pending,
    KeepLocal,
    KeepRemote,
    Merged,
    Ignored
}
