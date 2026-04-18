namespace CompanyBrain.Dashboard.Features.SharePoint.Data;

/// <summary>
/// Configuration for a synced SharePoint folder.
/// </summary>
public sealed class SyncedSharePointFolder
{
    public int Id { get; set; }

    /// <summary>
    /// Azure AD Tenant ID.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// SharePoint Site ID.
    /// </summary>
    public required string SiteId { get; set; }

    /// <summary>
    /// SharePoint Site display name (cached).
    /// </summary>
    public required string SiteName { get; set; }

    /// <summary>
    /// SharePoint Drive ID (document library).
    /// </summary>
    public required string DriveId { get; set; }

    /// <summary>
    /// SharePoint Drive name (cached).
    /// </summary>
    public required string DriveName { get; set; }

    /// <summary>
    /// Folder path within the drive (empty string = root).
    /// </summary>
    public string FolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Local folder path where files are mirrored.
    /// </summary>
    public required string LocalPath { get; set; }

    /// <summary>
    /// Delta link for incremental sync.
    /// </summary>
    public string? DeltaLink { get; set; }

    /// <summary>
    /// When sync was first configured.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the last successful sync completed.
    /// </summary>
    public DateTimeOffset? LastSyncedAtUtc { get; set; }

    /// <summary>
    /// Last sync error message, if any.
    /// </summary>
    public string? LastSyncError { get; set; }

    /// <summary>
    /// Whether sync is enabled for this folder.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Number of files currently synced.
    /// </summary>
    public int SyncedFileCount { get; set; }

    /// <summary>
    /// Total size of synced files in bytes.
    /// </summary>
    public long SyncedSizeBytes { get; set; }
}
