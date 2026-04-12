namespace CompanyBrain.Dashboard.Features.SharePoint.Data;

/// <summary>
/// Metadata for a synced SharePoint file (stored in SQLite FTS5 for full-text search).
/// </summary>
public sealed class SyncedSharePointFile
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the parent synced folder.
    /// </summary>
    public int SyncedFolderId { get; set; }

    /// <summary>
    /// SharePoint Drive Item ID.
    /// </summary>
    public required string DriveItemId { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Full local path to the file.
    /// </summary>
    public required string LocalPath { get; set; }

    /// <summary>
    /// Relative path within the SharePoint drive.
    /// </summary>
    public required string RemotePath { get; set; }

    /// <summary>
    /// MIME type of the file.
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// ETag for change detection.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// SharePoint last modified timestamp.
    /// </summary>
    public DateTimeOffset RemoteLastModifiedUtc { get; set; }

    /// <summary>
    /// Local file last modified timestamp.
    /// </summary>
    public DateTimeOffset LocalLastModifiedUtc { get; set; }

    /// <summary>
    /// When this file was first synced.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this file was last synced.
    /// </summary>
    public DateTimeOffset LastSyncedAtUtc { get; set; }

    /// <summary>
    /// Extracted text content for FTS5 search (for supported file types).
    /// </summary>
    public string? ExtractedContent { get; set; }

    /// <summary>
    /// Navigation property.
    /// </summary>
    public SyncedSharePointFolder? SyncedFolder { get; set; }
}
