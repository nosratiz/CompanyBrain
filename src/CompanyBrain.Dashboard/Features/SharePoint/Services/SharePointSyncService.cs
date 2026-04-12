using System.IO.Pipelines;
using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace CompanyBrain.Dashboard.Features.SharePoint.Services;

/// <summary>
/// Core SharePoint synchronization service using Microsoft Graph Delta Queries.
/// Handles full crawl, incremental sync, conflict detection, and file downloads.
/// </summary>
public sealed class SharePointSyncService(
    GraphClientFactory graphClientFactory,
    IDbContextFactory<SharePointDbContext> dbContextFactory,
    IOptions<SharePointSyncOptions> options,
    ILogger<SharePointSyncService> logger)
{
    private readonly SharePointSyncOptions _options = options.Value;

    /// <summary>
    /// Searches for SharePoint sites matching the query.
    /// </summary>
    public async Task<IReadOnlyList<SharePointSite>> SearchSitesAsync(
        string tenantId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var client = await graphClientFactory.GetBackgroundClientAsync(tenantId, cancellationToken);
        if (client is null)
        {
            logger.LogWarning("No Graph client available for tenant {TenantId}", tenantId);
            return [];
        }

        try
        {
            var searchResult = await client.Sites.GetAsync(config =>
            {
                config.QueryParameters.Search = $"\"{query}\"";
                config.QueryParameters.Select = ["id", "displayName", "webUrl", "description", "createdDateTime"];
            }, cancellationToken);

            if (searchResult?.Value is null)
                return [];

            return searchResult.Value
                .Where(s => s.Id is not null)
                .Select(s => new SharePointSite(
                    s.Id!,
                    s.DisplayName ?? "Unknown",
                    s.WebUrl ?? string.Empty,
                    s.Description,
                    s.CreatedDateTime ?? DateTimeOffset.MinValue))
                .ToList();
        }
        catch (ODataError ex)
        {
            logger.LogError(ex, "Graph API error searching sites: {Message}", ex.Error?.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the document libraries (drives) for a SharePoint site.
    /// </summary>
    public async Task<IReadOnlyList<SharePointDrive>> GetSiteDrivesAsync(
        string tenantId,
        string siteId,
        CancellationToken cancellationToken = default)
    {
        var client = await graphClientFactory.GetBackgroundClientAsync(tenantId, cancellationToken);
        if (client is null)
            return [];

        try
        {
            var drives = await client.Sites[siteId].Drives.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "name", "description", "driveType", "quota"];
            }, cancellationToken);

            if (drives?.Value is null)
                return [];

            return drives.Value
                .Where(d => d.Id is not null)
                .Select(d => new SharePointDrive(
                    d.Id!,
                    d.Name ?? "Documents",
                    d.Description,
                    d.DriveType ?? "documentLibrary",
                    d.Quota?.Total,
                    d.Quota?.Used,
                    siteId))
                .ToList();
        }
        catch (ODataError ex)
        {
            logger.LogError(ex, "Graph API error getting drives: {Message}", ex.Error?.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the children (files/folders) of a drive or folder.
    /// </summary>
    public async Task<IReadOnlyList<SharePointDriveItem>> GetDriveItemsAsync(
        string tenantId,
        string driveId,
        string? folderId = null,
        CancellationToken cancellationToken = default)
    {
        var client = await graphClientFactory.GetBackgroundClientAsync(tenantId, cancellationToken);
        if (client is null)
            return [];

        try
        {
            DriveItemCollectionResponse? items;
            var selectFields = new[] { "id", "name", "folder", "file", "size", "lastModifiedDateTime", "parentReference", "eTag" };

            if (string.IsNullOrEmpty(folderId) || folderId == "root")
            {
                items = await client.Drives[driveId].Items["root"].Children.GetAsync(config =>
                {
                    config.QueryParameters.Select = selectFields;
                }, cancellationToken);
            }
            else
            {
                items = await client.Drives[driveId].Items[folderId].Children.GetAsync(config =>
                {
                    config.QueryParameters.Select = selectFields;
                }, cancellationToken);
            }

            if (items?.Value is null)
                return [];

            return items.Value
                .Where(i => i.Id is not null)
                .Select(i => MapToDriveItem(i, driveId))
                .ToList();
        }
        catch (ODataError ex)
        {
            logger.LogError(ex, "Graph API error getting drive items: {Message}", ex.Error?.Message);
            throw;
        }
    }

    /// <summary>
    /// Configures a folder for local sync.
    /// </summary>
    public async Task<SyncedSharePointFolder> ConfigureSyncFolderAsync(
        string tenantId,
        string siteName,
        string siteId,
        string driveId,
        string driveName,
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Check if already configured
        var existing = await db.SyncedFolders
            .FirstOrDefaultAsync(f =>
                f.TenantId == tenantId &&
                f.SiteId == siteId &&
                f.DriveId == driveId &&
                f.FolderPath == folderPath,
                cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation("Sync folder already configured: {LocalPath}", existing.LocalPath);
            return existing;
        }

        // Build local path
        var safeSiteName = SanitizePathComponent(siteName);
        var safeDriverName = SanitizePathComponent(driveName);
        var localPath = Path.Combine(
            _options.LocalBasePath,
            tenantId,
            "SharePoint",
            safeSiteName,
            safeDriverName,
            SanitizePathComponent(folderPath));

        Directory.CreateDirectory(localPath);

        var syncedFolder = new SyncedSharePointFolder
        {
            TenantId = tenantId,
            SiteId = siteId,
            SiteName = siteName,
            DriveId = driveId,
            DriveName = driveName,
            FolderPath = folderPath,
            LocalPath = localPath
        };

        db.SyncedFolders.Add(syncedFolder);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Configured sync folder: {RemotePath} -> {LocalPath}",
            $"{siteName}/{driveName}/{folderPath}", localPath);

        return syncedFolder;
    }

    /// <summary>
    /// Performs a full or incremental sync for a configured folder.
    /// Uses delta queries for incremental updates.
    /// </summary>
    public async Task SyncFolderAsync(
        int syncedFolderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var folder = await db.SyncedFolders.FindAsync([syncedFolderId], cancellationToken);

        if (folder is null || !folder.IsEnabled)
        {
            logger.LogWarning("Sync folder {Id} not found or disabled", syncedFolderId);
            return;
        }

        var client = await graphClientFactory.GetBackgroundClientAsync(folder.TenantId, cancellationToken);
        if (client is null)
        {
            folder.LastSyncError = "No Graph client available";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(folder.DeltaLink))
            {
                // Full crawl
                await PerformFullCrawlAsync(db, folder, client, cancellationToken);
            }
            else
            {
                // Incremental sync using delta link
                await PerformDeltaSyncAsync(db, folder, client, cancellationToken);
            }

            folder.LastSyncedAtUtc = DateTimeOffset.UtcNow;
            folder.LastSyncError = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync failed for folder {FolderId}", syncedFolderId);
            folder.LastSyncError = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Performs a full crawl of the SharePoint folder.
    /// </summary>
    private async Task PerformFullCrawlAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        GraphServiceClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting full crawl for {Site}/{Drive}/{Folder}",
            folder.SiteName, folder.DriveName, folder.FolderPath);

        var response = await client.Drives[folder.DriveId].Items["root"]
            .Delta
            .GetAsDeltaGetResponseAsync(cancellationToken: cancellationToken);

        var allItems = new List<DriveItem>();
        var deltaLink = string.Empty;

        while (response is not null)
        {
            if (response.Value is not null)
            {
                allItems.AddRange(response.Value);
            }

            // Check for delta link
            if (response.OdataDeltaLink is not null)
            {
                deltaLink = response.OdataDeltaLink;
                break;
            }

            // Get next page
            if (response.OdataNextLink is not null)
            {
                response = await client.Drives[folder.DriveId].Items["root"]
                    .Delta
                    .WithUrl(response.OdataNextLink)
                    .GetAsDeltaGetResponseAsync(cancellationToken: cancellationToken);
            }
            else
            {
                break;
            }
        }

        // Process items (filter by folder path if not root)
        var relevantItems = FilterItemsByPath(allItems, folder.FolderPath);
        var downloadedCount = 0;
        long totalSize = 0;

        foreach (var item in relevantItems.Where(i => i.File is not null))
        {
            await ProcessDriveItemAsync(db, folder, item, isDelete: false, cancellationToken);
            downloadedCount++;
            totalSize += item.Size ?? 0;
        }

        folder.DeltaLink = deltaLink;
        folder.SyncedFileCount = downloadedCount;
        folder.SyncedSizeBytes = totalSize;

        logger.LogInformation("Full crawl complete: {Count} files, {Size} bytes",
            downloadedCount, totalSize);
    }

    /// <summary>
    /// Performs an incremental sync using the stored delta link.
    /// </summary>
    private async Task PerformDeltaSyncAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        GraphServiceClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting delta sync for {Site}/{Drive}/{Folder}",
            folder.SiteName, folder.DriveName, folder.FolderPath);

        var response = await client.Drives[folder.DriveId].Items["root"]
            .Delta
            .WithUrl(folder.DeltaLink!)
            .GetAsDeltaGetResponseAsync(cancellationToken: cancellationToken);

        var changedItems = new List<DriveItem>();
        var newDeltaLink = folder.DeltaLink;

        while (response is not null)
        {
            if (response.Value is not null)
            {
                changedItems.AddRange(response.Value);
            }

            if (response.OdataDeltaLink is not null)
            {
                newDeltaLink = response.OdataDeltaLink;
                break;
            }

            if (response.OdataNextLink is not null)
            {
                response = await client.Drives[folder.DriveId].Items["root"]
                    .Delta
                    .WithUrl(response.OdataNextLink)
                    .GetAsDeltaGetResponseAsync(cancellationToken: cancellationToken);
            }
            else
            {
                break;
            }
        }

        // Process changes
        var relevantItems = FilterItemsByPath(changedItems, folder.FolderPath);

        foreach (var item in relevantItems)
        {
            // Check if this is a deletion
            var isDelete = item.AdditionalData.ContainsKey("@removed");
            await ProcessDriveItemAsync(db, folder, item, isDelete, cancellationToken);
        }

        folder.DeltaLink = newDeltaLink;

        // Update counts
        folder.SyncedFileCount = await db.SyncedFiles
            .CountAsync(f => f.SyncedFolderId == folder.Id, cancellationToken);
        folder.SyncedSizeBytes = await db.SyncedFiles
            .Where(f => f.SyncedFolderId == folder.Id)
            .SumAsync(f => f.Size, cancellationToken);

        logger.LogInformation("Delta sync complete: {Count} changes processed", changedItems.Count);
    }

    /// <summary>
    /// Processes a single drive item (download, update, or delete).
    /// </summary>
    private async Task ProcessDriveItemAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        DriveItem item,
        bool isDelete,
        CancellationToken cancellationToken)
    {
        if (item.Id is null || item.Name is null)
            return;

        // Skip folders for file processing
        if (item.Folder is not null)
            return;

        var remotePath = item.ParentReference?.Path ?? "";
        remotePath = Path.Combine(remotePath, item.Name);

        var localPath = Path.Combine(folder.LocalPath, SanitizePathComponent(remotePath));

        var existingFile = await db.SyncedFiles
            .FirstOrDefaultAsync(f => f.DriveItemId == item.Id, cancellationToken);

        if (isDelete)
        {
            // Handle deletion
            if (existingFile is not null)
            {
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }

                db.SyncedFiles.Remove(existingFile);
                logger.LogInformation("Deleted file: {Path}", localPath);
            }

            return;
        }

        // Check for conflict
        if (existingFile is not null && File.Exists(localPath))
        {
            var localFileInfo = new FileInfo(localPath);
            var remoteLastModified = item.LastModifiedDateTime ?? DateTimeOffset.MinValue;

            if (localFileInfo.LastWriteTimeUtc > existingFile.LocalLastModifiedUtc &&
                localFileInfo.LastWriteTimeUtc > remoteLastModified)
            {
                // Local file is newer - create conflict
                await CreateConflictAsync(db, existingFile, localPath, remotePath,
                    folder.DriveId, item.Id, localFileInfo.LastWriteTimeUtc, remoteLastModified,
                    cancellationToken);
                return;
            }
        }

        // Download file using Stream for low memory usage
        await DownloadFileAsync(item, localPath, cancellationToken);

        // Update or create file record
        if (existingFile is null)
        {
            existingFile = new SyncedSharePointFile
            {
                SyncedFolderId = folder.Id,
                DriveItemId = item.Id,
                FileName = item.Name,
                LocalPath = localPath,
                RemotePath = remotePath
            };
            db.SyncedFiles.Add(existingFile);
        }

        existingFile.Size = item.Size ?? 0;
        existingFile.MimeType = item.File?.MimeType;
        existingFile.ETag = item.ETag;
        existingFile.RemoteLastModifiedUtc = item.LastModifiedDateTime ?? DateTimeOffset.UtcNow;
        existingFile.LocalLastModifiedUtc = new FileInfo(localPath).LastWriteTimeUtc;
        existingFile.LastSyncedAtUtc = DateTimeOffset.UtcNow;

        // Extract text content for FTS (for supported types)
        existingFile.ExtractedContent = await ExtractTextContentAsync(localPath, item.File?.MimeType);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Downloads a file using System.IO.Pipelines for low memory usage.
    /// </summary>
    private async Task DownloadFileAsync(
        DriveItem item,
        string localPath,
        CancellationToken cancellationToken)
    {
        // Get download URL from additional data
        if (!item.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var downloadUrlObj) ||
            downloadUrlObj is not string downloadUrl)
        {
            logger.LogWarning("No download URL for item {Id}", item.Id);
            return;
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(localPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Use a static HttpClient for better connection pooling
        await using var response = await SharedHttpClient.Instance.GetStreamAsync(downloadUrl, cancellationToken);

        // Use PipeWriter for efficient buffered writing
        await using var fileStream = new FileStream(
            localPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: _options.DownloadChunkSize,
            useAsync: true);

        var pipe = new Pipe(new PipeOptions(
            minimumSegmentSize: _options.DownloadChunkSize));

        var writeTask = WriteToFileAsync(pipe.Reader, fileStream, cancellationToken);
        var readTask = ReadFromStreamAsync(response, pipe.Writer, cancellationToken);

        await Task.WhenAll(readTask, writeTask);

        logger.LogDebug("Downloaded file: {Path} ({Size} bytes)", localPath, item.Size);
    }

    /// <summary>
    /// Shared HttpClient for file downloads to avoid socket exhaustion.
    /// </summary>
    private static class SharedHttpClient
    {
        public static readonly HttpClient Instance = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
    }

    private static async Task ReadFromStreamAsync(
        Stream source,
        PipeWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var memory = writer.GetMemory(4096);
                var bytesRead = await source.ReadAsync(memory, cancellationToken);

                if (bytesRead == 0)
                    break;

                writer.Advance(bytesRead);
                var result = await writer.FlushAsync(cancellationToken);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    private static async Task WriteToFileAsync(
        PipeReader reader,
        Stream destination,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    await destination.WriteAsync(segment, cancellationToken);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    /// <summary>
    /// Creates a sync conflict record.
    /// </summary>
    private async Task CreateConflictAsync(
        SharePointDbContext db,
        SyncedSharePointFile syncedFile,
        string localPath,
        string remotePath,
        string driveId,
        string itemId,
        DateTimeOffset localLastModified,
        DateTimeOffset remoteLastModified,
        CancellationToken cancellationToken)
    {
        var conflict = new SharePointSyncConflict
        {
            SyncedFileId = syncedFile.Id,
            LocalPath = localPath,
            RemotePath = remotePath,
            DriveId = driveId,
            ItemId = itemId,
            LocalLastModified = localLastModified,
            RemoteLastModified = remoteLastModified,
            Status = "Pending"
        };

        db.SyncConflicts.Add(conflict);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Conflict detected: {Path} (local: {Local}, remote: {Remote})",
            localPath, localLastModified, remoteLastModified);
    }

    /// <summary>
    /// Extracts text content from supported file types for FTS indexing.
    /// </summary>
    private static async Task<string?> ExtractTextContentAsync(string filePath, string? mimeType)
    {
        try
        {
            return mimeType switch
            {
                "text/plain" or "text/markdown" or "text/csv" =>
                    await File.ReadAllTextAsync(filePath),

                "text/html" or "text/xml" =>
                    StripHtmlTags(await File.ReadAllTextAsync(filePath)),

                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string StripHtmlTags(string html)
    {
        // Simple HTML stripping - in production, use a proper HTML parser
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
    }

    /// <summary>
    /// Filters drive items by folder path.
    /// </summary>
    private static IEnumerable<DriveItem> FilterItemsByPath(
        IEnumerable<DriveItem> items,
        string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return items;

        return items.Where(item =>
        {
            var itemPath = item.ParentReference?.Path ?? "";
            return itemPath.Contains(folderPath, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Maps a Graph DriveItem to our model.
    /// </summary>
    private static SharePointDriveItem MapToDriveItem(DriveItem item, string driveId)
    {
        string? downloadUrl = null;
        if (item.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var url))
        {
            downloadUrl = url as string;
        }

        return new SharePointDriveItem(
            item.Id!,
            item.Name ?? "Unknown",
            item.ParentReference?.Path,
            item.Folder is not null,
            item.Size,
            item.File?.MimeType,
            item.LastModifiedDateTime,
            driveId,
            downloadUrl,
            item.ETag);
    }

    /// <summary>
    /// Sanitizes a path component for safe filesystem use.
    /// </summary>
    private static string SanitizePathComponent(string path)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", path.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Trim();
    }
}
