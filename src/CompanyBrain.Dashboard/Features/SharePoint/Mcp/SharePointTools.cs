using System.ComponentModel;
using System.Text;
using CompanyBrain.Dashboard.Features.SharePoint.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Features.SharePoint.Mcp;

/// <summary>
/// MCP tools for searching and accessing the local SharePoint mirror.
/// Enables AI agents to query synced SharePoint content using FTS5 full-text search.
/// </summary>
[McpServerToolType]
public sealed class SharePointTools(
    IDbContextFactory<SharePointDbContext> dbContextFactory,
    ILogger<SharePointTools> logger)
{
    [McpServerTool, Description("Searches the locally mirrored SharePoint content using full-text search. Returns matching files with paths and content snippets.")]
    public async Task<string> SearchSharePoint(
        [Description("Search query to find in SharePoint documents.")] string query,
        [Description("Maximum number of results to return. Defaults to 10.")] int maxResults,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: Search query is required.";
        }

        maxResults = maxResults <= 0 ? 10 : Math.Min(maxResults, 50);

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var results = await db.SearchFilesAsync(query, maxResults, cancellationToken);

            if (results.Count == 0)
            {
                return $"No SharePoint documents found matching '{query}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} SharePoint document(s) matching '{query}':");
            sb.AppendLine();

            foreach (var file in results)
            {
                var folder = file.SyncedFolder;
                sb.AppendLine($"## {file.FileName}");
                sb.AppendLine($"- **Site:** {folder?.SiteName ?? "Unknown"}");
                sb.AppendLine($"- **Library:** {folder?.DriveName ?? "Unknown"}");
                sb.AppendLine($"- **Path:** {file.RemotePath}");
                sb.AppendLine($"- **Local:** {file.LocalPath}");
                sb.AppendLine($"- **Size:** {FormatBytes(file.Size)}");
                sb.AppendLine($"- **Modified:** {file.RemoteLastModifiedUtc:g}");
                sb.AppendLine($"- **MCP Resource:** nexus://{folder?.TenantId}/sharepoint/{file.Id}");
                sb.AppendLine();

                // Include content snippet if available
                if (!string.IsNullOrEmpty(file.ExtractedContent))
                {
                    var snippet = GetContentSnippet(file.ExtractedContent, query, 200);
                    sb.AppendLine($"> {snippet}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SharePoint search failed for query: {Query}", query);
            return $"Error searching SharePoint: {ex.Message}";
        }
    }

    [McpServerTool, Description("Lists all locally synced SharePoint folders with sync status.")]
    public async Task<string> ListSyncedFolders(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var folders = await db.SyncedFolders
                .OrderBy(f => f.SiteName)
                .ThenBy(f => f.DriveName)
                .ToListAsync(cancellationToken);

            if (folders.Count == 0)
            {
                return "No SharePoint folders are currently synced. Use the SharePoint Connector UI to configure sync.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Synced SharePoint Folders ({folders.Count}):");
            sb.AppendLine();

            foreach (var folder in folders)
            {
                var status = !folder.IsEnabled ? "Disabled" :
                    string.IsNullOrEmpty(folder.LastSyncError) ? "Active" : "Error";

                sb.AppendLine($"## {folder.SiteName} / {folder.DriveName}");
                sb.AppendLine($"- **Path:** {(string.IsNullOrEmpty(folder.FolderPath) ? "/" : folder.FolderPath)}");
                sb.AppendLine($"- **Local:** {folder.LocalPath}");
                sb.AppendLine($"- **Files:** {folder.SyncedFileCount}");
                sb.AppendLine($"- **Size:** {FormatBytes(folder.SyncedSizeBytes)}");
                sb.AppendLine($"- **Status:** {status}");
                sb.AppendLine($"- **Last Sync:** {(folder.LastSyncedAtUtc.HasValue ? folder.LastSyncedAtUtc.Value.ToString("g") : "Never")}");

                if (!string.IsNullOrEmpty(folder.LastSyncError))
                {
                    sb.AppendLine($"- **Error:** {folder.LastSyncError}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list synced folders");
            return $"Error listing synced folders: {ex.Message}";
        }
    }

    [McpServerTool, Description("Gets the content of a specific SharePoint file by its local path or remote path.")]
    public async Task<string> GetSharePointFileContent(
        [Description("Local file path or remote SharePoint path of the file.")] string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Error: File path is required.";
        }

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var file = await db.SyncedFiles
                .Include(f => f.SyncedFolder)
                .FirstOrDefaultAsync(f =>
                    f.LocalPath.Contains(path) || f.RemotePath.Contains(path),
                    cancellationToken);

            if (file is null)
            {
                return $"SharePoint file not found: {path}";
            }

            if (!File.Exists(file.LocalPath))
            {
                return $"Local file no longer exists: {file.LocalPath}. The file may have been deleted or sync is required.";
            }

            // Return extracted content if available, otherwise read file
            if (!string.IsNullOrEmpty(file.ExtractedContent))
            {
                return FormatFileResponse(file, file.ExtractedContent);
            }

            // Try to read text content directly
            try
            {
                var content = await File.ReadAllTextAsync(file.LocalPath, cancellationToken);
                return FormatFileResponse(file, content);
            }
            catch
            {
                return $"Cannot read file content (binary file): {file.FileName}\n\nLocal path: {file.LocalPath}";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get SharePoint file content: {Path}", path);
            return $"Error getting file content: {ex.Message}";
        }
    }

    [McpServerTool, Description("Lists pending sync conflicts that require manual resolution.")]
    public async Task<string> ListSyncConflicts(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var conflicts = await db.SyncConflicts
                .Where(c => c.Status == "Pending")
                .OrderByDescending(c => c.Id)
                .Include(c => c.SyncedFile)
                .ThenInclude(f => f!.SyncedFolder)
                .ToListAsync(cancellationToken);

            if (conflicts.Count == 0)
            {
                return "No pending sync conflicts.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Pending Sync Conflicts ({conflicts.Count}):");
            sb.AppendLine();
            sb.AppendLine("These files have local changes that conflict with SharePoint. Manual resolution required.");
            sb.AppendLine();

            foreach (var conflict in conflicts)
            {
                sb.AppendLine($"## {Path.GetFileName(conflict.LocalPath)}");
                sb.AppendLine($"- **Local Modified:** {conflict.LocalLastModified:g}");
                sb.AppendLine($"- **Remote Modified:** {conflict.RemoteLastModified:g}");
                sb.AppendLine($"- **Detected:** {conflict.DetectedAtUtc:g}");
                sb.AppendLine($"- **Local Path:** {conflict.LocalPath}");
                sb.AppendLine($"- **Remote Path:** {conflict.RemotePath}");

                if (conflict.SyncedFile?.SyncedFolder is not null)
                {
                    sb.AppendLine($"- **Site:** {conflict.SyncedFile.SyncedFolder.SiteName}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list sync conflicts");
            return $"Error listing conflicts: {ex.Message}";
        }
    }

    private static string FormatFileResponse(SyncedSharePointFile file, string content)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {file.FileName}");
        sb.AppendLine();
        sb.AppendLine($"- **Site:** {file.SyncedFolder?.SiteName}");
        sb.AppendLine($"- **Library:** {file.SyncedFolder?.DriveName}");
        sb.AppendLine($"- **Remote Path:** {file.RemotePath}");
        sb.AppendLine($"- **Local Path:** {file.LocalPath}");
        sb.AppendLine($"- **Size:** {FormatBytes(file.Size)}");
        sb.AppendLine($"- **Modified:** {file.RemoteLastModifiedUtc:g}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(content);

        return sb.ToString();
    }

    private static string GetContentSnippet(string content, string query, int maxLength)
    {
        // Find query in content and return surrounding text
        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            // Return beginning of content
            return content.Length <= maxLength
                ? content
                : content[..maxLength] + "...";
        }

        var start = Math.Max(0, index - maxLength / 2);
        var end = Math.Min(content.Length, start + maxLength);

        var snippet = content[start..end];
        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";

        return snippet;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
