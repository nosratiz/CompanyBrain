namespace CompanyBrain.Dashboard.Features.SharePoint.Data;

/// <summary>
/// Database entity for sync conflicts requiring manual resolution.
/// </summary>
public sealed class SharePointSyncConflict
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the synced file.
    /// </summary>
    public int SyncedFileId { get; set; }

    /// <summary>
    /// Local file path.
    /// </summary>
    public required string LocalPath { get; set; }

    /// <summary>
    /// Remote path in SharePoint.
    /// </summary>
    public required string RemotePath { get; set; }

    /// <summary>
    /// Drive ID.
    /// </summary>
    public required string DriveId { get; set; }

    /// <summary>
    /// Drive Item ID.
    /// </summary>
    public required string ItemId { get; set; }

    /// <summary>
    /// Local file last modified.
    /// </summary>
    public DateTimeOffset LocalLastModified { get; set; }

    /// <summary>
    /// Remote file last modified.
    /// </summary>
    public DateTimeOffset RemoteLastModified { get; set; }

    /// <summary>
    /// Conflict status.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// When conflict was detected.
    /// </summary>
    public DateTimeOffset DetectedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When conflict was resolved.
    /// </summary>
    public DateTimeOffset? ResolvedAtUtc { get; set; }

    /// <summary>
    /// Navigation property.
    /// </summary>
    public SyncedSharePointFile? SyncedFile { get; set; }
}
