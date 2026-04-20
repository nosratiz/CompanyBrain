using System.IO.Pipelines;
using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using Microsoft.EntityFrameworkCore;
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
    SharePointSettingsProvider settingsProvider,
    ILogger<SharePointSyncService> logger)
{

    /// <summary>
    /// Gets a delegated Graph client for interactive user operations (search, browse).
    /// Uses the token acquired during "Connect to SharePoint" OAuth flow.
    /// </summary>
    private async Task<GraphServiceClient?> GetDelegatedClientAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        return await graphClientFactory.CreateDelegatedClientAsync(tenantId, cancellationToken);
    }

    /// <summary>
    /// Searches for SharePoint sites matching the query.
    /// </summary>
    public async Task<IReadOnlyList<SharePointSite>> SearchSitesAsync(
        string tenantId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var client = await GetDelegatedClientAsync(tenantId, cancellationToken);
        if (client is null)
        {
            logger.LogWarning("No Graph client available for tenant {TenantId}", tenantId);
            return [];
        }

        try
        {
            var results = await SearchSitesCoreAsync(client, query, cancellationToken);

            if (results.Count != 0)
                return [.. results.Values];

            return await FallbackSiteSearchAsync(client, query, cancellationToken);
        }
        catch (ODataError ex)
        {
            logger.LogError(ex, "Graph API error searching sites: {Message}", ex.Error?.Message);
            throw;
        }
    }

    private async Task<Dictionary<string, SharePointSite>> SearchSitesCoreAsync(
        GraphServiceClient client,
        string query,
        CancellationToken ct)
    {
        var sitesTask = client.Sites.GetAsync(config =>
        {
            config.QueryParameters.Search = query;
            config.QueryParameters.Select = ["id", "displayName", "webUrl", "description", "createdDateTime"];
        }, ct);

        var groupsTask = client.Groups.GetAsync(config =>
        {
            config.QueryParameters.Search = $"\"displayName:{query}\"";
            config.QueryParameters.Filter = "groupTypes/any(c:c eq 'Unified')";
            config.QueryParameters.Select = ["id", "displayName"];
            config.QueryParameters.Count = true;
            config.Headers.Add("ConsistencyLevel", "eventual");
        }, ct);

        await Task.WhenAll(sitesTask, groupsTask);

        var results = new Dictionary<string, SharePointSite>();

        foreach (var s in sitesTask.Result?.Value ?? [])
        {
            if (s.Id is null) continue;
            results[s.Id] = new SharePointSite(s.Id, s.DisplayName ?? "Unknown",
                s.WebUrl ?? string.Empty, s.Description,
                s.CreatedDateTime ?? DateTimeOffset.MinValue);
        }

        var groupSiteTasks = (groupsTask.Result?.Value ?? [])
            .Where(g => g.Id is not null)
            .Select(g => ResolveGroupSiteAsync(client, g.Id!, g.DisplayName, ct));

        var groupSites = await Task.WhenAll(groupSiteTasks);
        foreach (var site in groupSites)
        {
            if (site is not null && !results.ContainsKey(site.Id))
                results[site.Id] = site;
        }

        return results;
    }

    private async Task<IReadOnlyList<SharePointSite>> FallbackSiteSearchAsync(
        GraphServiceClient client,
        string query,
        CancellationToken ct)
    {
        logger.LogInformation("Search returned no results, falling back to full site listing");
        var allSites = await client.Sites.GetAsync(config =>
        {
            config.QueryParameters.Search = "*";
            config.QueryParameters.Select = ["id", "displayName", "webUrl", "description", "createdDateTime"];
        }, ct);

        return (allSites?.Value ?? [])
            .Where(s => s.Id is not null &&
                        s.DisplayName != null &&
                        s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(s => new SharePointSite(s.Id!, s.DisplayName ?? "Unknown",
                s.WebUrl ?? string.Empty, s.Description,
                s.CreatedDateTime ?? DateTimeOffset.MinValue))
            .ToList();
    }

    private async Task<SharePointSite?> ResolveGroupSiteAsync(
        GraphServiceClient client,
        string groupId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        try
        {
            var site = await client.Groups[groupId].Sites["root"].GetAsync(
                config => config.QueryParameters.Select = ["id", "displayName", "webUrl", "description", "createdDateTime"],
                cancellationToken);

            if (site?.Id is null) return null;

            return new SharePointSite(
                site.Id,
                site.DisplayName ?? displayName ?? "Unknown",
                site.WebUrl ?? string.Empty,
                site.Description,
                site.CreatedDateTime ?? DateTimeOffset.MinValue);
        }
        catch (ODataError ex)
        {
            logger.LogDebug("Could not resolve site for group {GroupId}: {Message}", groupId, ex.Error?.Message);
            return null;
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
        var client = await GetDelegatedClientAsync(tenantId, cancellationToken);
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
        var client = await GetDelegatedClientAsync(tenantId, cancellationToken);
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
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        var safeSiteName = SanitizePathComponent(siteName);
        var safeDriverName = SanitizePathComponent(driveName);
        var localPath = Path.Combine(
            options.LocalBasePath,
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

        var client = await ResolveGraphClientAsync(folder, db, cancellationToken);
        if (client is null)
            return;

        try
        {
            await ExecuteSyncStrategyAsync(db, folder, client, syncedFolderId, cancellationToken);
            folder.LastSyncedAtUtc = DateTimeOffset.UtcNow;
            folder.LastSyncError = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ODataError ex)
        {
            await HandleODataSyncErrorAsync(db, folder, syncedFolderId, ex);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync failed for folder {FolderId}", syncedFolderId);
            folder.LastSyncError = ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<GraphServiceClient?> ResolveGraphClientAsync(
        SyncedSharePointFolder folder,
        SharePointDbContext db,
        CancellationToken ct)
    {
        var delegatedClient = await graphClientFactory.CreateDelegatedClientAsync(folder.TenantId, ct);
        var client = delegatedClient
                  ?? await graphClientFactory.GetBackgroundClientAsync(folder.TenantId, ct);

        if (client is not null)
        {
            logger.LogInformation("Syncing folder {FolderId} using {ClientType} client",
                folder.Id, delegatedClient is not null ? "delegated" : "app-only");
            return client;
        }

        folder.LastSyncError = "No Graph client available — reconnect SharePoint to refresh the token";
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task ExecuteSyncStrategyAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        GraphServiceClient client,
        int syncedFolderId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(folder.DeltaLink))
        {
            await PerformFullCrawlAsync(db, folder, client, ct);
            return;
        }

        try
        {
            await PerformDeltaSyncAsync(db, folder, client, ct);
        }
        catch (ODataError ex) when (ex.Error?.Code is "generalException" or "resyncRequired" or "deltaTokenExpired")
        {
            logger.LogWarning("Delta link invalid ({Code}) for folder {FolderId}, resetting to full crawl",
                ex.Error.Code, syncedFolderId);
            folder.DeltaLink = null;
            folder.SyncedFileCount = 0;
            folder.SyncedSizeBytes = 0;
            await PerformFullCrawlAsync(db, folder, client, ct);
        }
    }

    private async Task HandleODataSyncErrorAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        int syncedFolderId,
        ODataError ex)
    {
        var code = ex.Error?.Code ?? "unknown";
        var message = ex.Error?.Message ?? ex.Message;
        var innerCode = ex.Error?.InnerError?.AdditionalData?.TryGetValue("code", out var ic) == true ? ic?.ToString() : null;
        var details = innerCode is not null ? $" (inner: {innerCode})" : "";

        logger.LogError(ex, "Sync failed for folder {FolderId} — Graph error {Code}: {Message}{Details}. " +
            "ResponseStatusCode={StatusCode}. " +
            "Ensure the authenticated user has Sites.Read.All and Files.Read.All permissions, " +
            "and that admin consent has been granted for the tenant.",
            syncedFolderId, code, message, details, ex.ResponseStatusCode);

        folder.LastSyncError = $"[{code}] {message}{details}";
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Performs a full crawl of the SharePoint folder.
    /// </summary>
    /// <summary>
    /// Extracts the drive-relative path from a Graph ParentReference.Path.
    /// e.g. "/drives/{id}/root:/General/Docs" → "General/Docs"
    /// </summary>
    private static string GetRelativeParentPath(string? graphPath)
    {
        if (string.IsNullOrEmpty(graphPath)) return string.Empty;
        var marker = "/root:";
        var idx = graphPath.IndexOf(marker, StringComparison.Ordinal);
        return idx < 0 ? string.Empty : graphPath[(idx + marker.Length)..].TrimStart('/');
    }

    /// <summary>
    /// Builds a safe local path from a drive-relative path, sanitizing each segment.
    /// </summary>
    private static string BuildLocalPath(string basePath, string relativeParent, string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string Sanitize(string s) => string.Join("_", s.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();

        var parts = relativeParent
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Sanitize)
            .Append(Sanitize(fileName))
            .ToArray();

        return Path.Combine([basePath, .. parts]);
    }

    private async Task PerformFullCrawlAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        GraphServiceClient client,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting full crawl for {Site}/{Drive}/{Folder}",
            folder.SiteName, folder.DriveName, folder.FolderPath);

        var (allItems, deltaLink) = await FetchAllDeltaItemsAsync(client, folder, cancellationToken);

        logger.LogInformation("Delta returned {Total} total items (folders+files)", allItems.Count);
        LogDeltaItemsDebug(allItems);

        var fileItems = FilterItemsByPath(allItems, folder.FolderPath)
            .Where(i => i.File is not null)
            .ToList();

        logger.LogInformation("After filter '{FolderPath}': {Files} files",
            folder.FolderPath, fileItems.Count);

        var (downloadedCount, totalSize) = await DownloadFilteredFilesAsync(
            db, folder, client, fileItems, cancellationToken);

        folder.DeltaLink = deltaLink;
        folder.SyncedFileCount = downloadedCount;
        folder.SyncedSizeBytes = totalSize;

        logger.LogInformation("Full crawl complete: {Downloaded}/{Total} files downloaded",
            downloadedCount, fileItems.Count);
    }

    private async Task<(List<DriveItem> Items, string DeltaLink)> FetchAllDeltaItemsAsync(
        GraphServiceClient client,
        SyncedSharePointFolder folder,
        CancellationToken ct)
    {
        logger.LogInformation("Delta query: driveId={DriveId}", folder.DriveId);

        Microsoft.Graph.Drives.Item.Items.Item.Delta.DeltaGetResponse? response;
        try
        {
            response = await client.Drives[folder.DriveId].Items["root"]
                .Delta
                .GetAsDeltaGetResponseAsync(cancellationToken: ct);
        }
        catch (ODataError ex) when (ex.Error?.Code is "generalException" or "accessDenied" or "unauthenticated"
                                     || ex.ResponseStatusCode is 403 or 401)
        {
            logger.LogWarning(ex,
                "Delta API returned {Code}/{StatusCode} for drive {DriveId}. " +
                "This usually means the authenticated account lacks permission to enumerate the drive. " +
                "Ensure the user has access to the SharePoint site and that Sites.Read.All has admin consent.",
                ex.Error?.Code, ex.ResponseStatusCode, folder.DriveId);
            throw;
        }

        var allItems = new List<DriveItem>();
        var deltaLink = string.Empty;

        while (response is not null)
        {
            if (response.Value is not null)
                allItems.AddRange(response.Value);

            if (response.OdataDeltaLink is not null)
            {
                deltaLink = response.OdataDeltaLink;
                break;
            }

            if (response.OdataNextLink is null)
                break;

            response = await client.Drives[folder.DriveId].Items["root"]
                .Delta
                .WithUrl(response.OdataNextLink)
                .GetAsDeltaGetResponseAsync(cancellationToken: ct);
        }

        return (allItems, deltaLink);
    }

    private void LogDeltaItemsDebug(List<DriveItem> items)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
            return;

        foreach (var dbg in items)
            logger.LogDebug("  Item: name={Name} isFile={IsFile} parentPath={ParentPath}",
                dbg.Name, dbg.File is not null, dbg.ParentReference?.Path);
    }

    private async Task<(int Downloaded, long TotalSize)> DownloadFilteredFilesAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        GraphServiceClient client,
        List<DriveItem> fileItems,
        CancellationToken ct)
    {
        var downloadedCount = 0;
        var totalSize = 0L;

        foreach (var item in fileItems)
        {
            var hasDownloadUrl = item.AdditionalData.ContainsKey("@microsoft.graph.downloadUrl");
            logger.LogInformation("Processing file: {Name} (id={Id}, size={Size}, hasDownloadUrl={HasUrl})",
                item.Name, item.Id, item.Size, hasDownloadUrl);

            var downloaded = await ProcessDriveItemAsync(db, folder, client, item, isDelete: false, ct);
            if (!downloaded)
            {
                logger.LogWarning("Skipped (download failed or conflict): {Name}", item.Name);
                continue;
            }

            downloadedCount++;
            totalSize += item.Size ?? 0;
            logger.LogInformation("Downloaded: {Name}", item.Name);
        }

        return (downloadedCount, totalSize);
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
            var isDelete = item.AdditionalData.ContainsKey("@removed");
            await ProcessDriveItemAsync(db, folder, client, item, isDelete, cancellationToken);
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
    /// Processes a single drive item (download, update, or delete). Returns true if a file was downloaded/updated.
    /// </summary>
    private async Task<bool> ProcessDriveItemAsync(
        SharePointDbContext db,
        SyncedSharePointFolder folder,
        GraphServiceClient client,
        DriveItem item,
        bool isDelete,
        CancellationToken cancellationToken)
    {
        if (item.Id is null || item.Name is null || item.Folder is not null)
            return false;

        var relativeParent = GetRelativeParentPath(item.ParentReference?.Path);
        var localPath = BuildLocalPath(folder.LocalPath, relativeParent, item.Name);
        var remotePath = string.IsNullOrEmpty(relativeParent) ? item.Name : $"{relativeParent}/{item.Name}";

        var existingFile = await db.SyncedFiles
            .FirstOrDefaultAsync(f => f.DriveItemId == item.Id, cancellationToken);

        if (isDelete)
            return await HandleDeleteAsync(db, existingFile, localPath, cancellationToken);

        if (HasLocalConflict(existingFile, localPath, item))
        {
            await CreateConflictAsync(db, existingFile!, localPath, remotePath,
                folder.DriveId, item.Id, new FileInfo(localPath).LastWriteTimeUtc,
                item.LastModifiedDateTime ?? DateTimeOffset.MinValue, cancellationToken);
            return false;
        }

        var downloaded = await DownloadFileAsync(client, folder.DriveId, item, localPath, cancellationToken);
        if (!downloaded)
            return false;

        await UpsertSyncedFileAsync(db, existingFile, folder, item, localPath, remotePath, cancellationToken);
        return true;
    }

    private async Task<bool> HandleDeleteAsync(
        SharePointDbContext db,
        SyncedSharePointFile? existingFile,
        string localPath,
        CancellationToken ct)
    {
        if (existingFile is null)
            return false;

        if (File.Exists(localPath)) File.Delete(localPath);
        db.SyncedFiles.Remove(existingFile);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted file: {Path}", localPath);
        return false;
    }

    private static bool HasLocalConflict(
        SyncedSharePointFile? existingFile,
        string localPath,
        DriveItem item)
    {
        if (existingFile is null || !File.Exists(localPath))
            return false;

        var localFileInfo = new FileInfo(localPath);
        var remoteLastModified = item.LastModifiedDateTime ?? DateTimeOffset.MinValue;

        return localFileInfo.LastWriteTimeUtc > existingFile.LocalLastModifiedUtc &&
               localFileInfo.LastWriteTimeUtc > remoteLastModified;
    }

    private async Task UpsertSyncedFileAsync(
        SharePointDbContext db,
        SyncedSharePointFile? existingFile,
        SyncedSharePointFolder folder,
        DriveItem item,
        string localPath,
        string remotePath,
        CancellationToken ct)
    {
        if (existingFile is null)
        {
            existingFile = new SyncedSharePointFile
            {
                SyncedFolderId = folder.Id,
                DriveItemId = item.Id!,
                FileName = item.Name!,
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
        existingFile.ExtractedContent = await ExtractTextContentAsync(localPath, item.File?.MimeType);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Downloads a file to localPath. Tries the pre-signed download URL first, then falls back
    /// to the Graph content endpoint so the Delta API's missing @downloadUrl doesn't block sync.
    /// Returns false if download could not be completed.
    /// </summary>
    private async Task<bool> DownloadFileAsync(
        GraphServiceClient client,
        string driveId,
        DriveItem item,
        string localPath,
        CancellationToken cancellationToken)
    {
        var syncOptions = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);

        logger.LogInformation("Downloading '{Name}' → {LocalPath}", item.Name, localPath);
        EnsureDirectoryExists(localPath);

        var contentStream = await AcquireContentStreamAsync(client, driveId, item, cancellationToken);
        if (contentStream is null)
            return false;

        await using var _ = contentStream;
        await using var fileStream = new FileStream(
            localPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: syncOptions.DownloadChunkSize, useAsync: true);

        var pipe = new Pipe(new PipeOptions(
            minimumSegmentSize: syncOptions.DownloadChunkSize));

        var writeTask = WriteToFileAsync(pipe.Reader, fileStream, cancellationToken);
        var readTask = ReadFromStreamAsync(contentStream, pipe.Writer, cancellationToken);

        await Task.WhenAll(readTask, writeTask);

        logger.LogDebug("Downloaded {Name} to {Path} ({Size} bytes)", item.Name, localPath, item.Size);
        return true;
    }

    private void EnsureDirectoryExists(string localPath)
    {
        var directory = Path.GetDirectoryName(localPath);
        if (directory is null)
            return;

        Directory.CreateDirectory(directory);
        logger.LogInformation("Target directory: {Dir} (exists={Exists})",
            directory, Directory.Exists(directory));
    }

    private async Task<Stream?> AcquireContentStreamAsync(
        GraphServiceClient client,
        string driveId,
        DriveItem item,
        CancellationToken ct)
    {
        try
        {
            if (item.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out var urlObj) &&
                urlObj is string downloadUrl)
            {
                logger.LogInformation("Using pre-signed URL for {Name}", item.Name);
                return await SharedHttpClient.Instance.GetStreamAsync(downloadUrl, ct);
            }

            logger.LogInformation("No pre-signed URL for {Name} — using Graph content endpoint", item.Name);
            var stream = await client.Drives[driveId].Items[item.Id!].Content
                .GetAsync(cancellationToken: ct);

            if (stream is null)
            {
                logger.LogWarning("Graph content endpoint returned null for {Name} (id={Id})", item.Name, item.Id);
                return null;
            }

            return stream;
        }
        catch (ODataError ex)
        {
            logger.LogError("Content download failed for {Name}: [{Code}] {Message}",
                item.Name, ex.Error?.Code, ex.Error?.Message ?? ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError("Content download failed for {Name}: {Message}", item.Name, ex.Message);
            return null;
        }
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
