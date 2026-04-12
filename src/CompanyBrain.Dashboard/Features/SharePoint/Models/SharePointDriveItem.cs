namespace CompanyBrain.Dashboard.Features.SharePoint.Models;

/// <summary>
/// Represents a file or folder from a SharePoint Drive.
/// </summary>
public sealed record SharePointDriveItem(
    string Id,
    string Name,
    string? Path,
    bool IsFolder,
    long? Size,
    string? MimeType,
    DateTimeOffset? LastModifiedDateTime,
    string DriveId,
    string? DownloadUrl,
    string? ETag);
