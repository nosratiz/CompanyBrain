using CompanyBrain.Dashboard.Features.SharePoint.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Features.SharePoint.Mcp;

/// <summary>
/// MCP Resource handlers for SharePoint content.
/// Maps synced SharePoint files to MCP resources using the nexus://{tenant}/sharepoint/{path} URI template.
/// </summary>
public sealed class SharePointResourceHandlers(
    IDbContextFactory<SharePointDbContext> dbContextFactory,
    ILogger<SharePointResourceHandlers> logger)
{
    private const string ResourceUriPrefix = "nexus://";
    private const string SharePointPath = "/sharepoint/";

    /// <summary>
    /// Lists all available SharePoint resources.
    /// </summary>
    public async Task<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> context,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var files = await db.SyncedFiles
            .Include(f => f.SyncedFolder)
            .Where(f => f.SyncedFolder != null && f.SyncedFolder.IsEnabled)
            .OrderBy(f => f.SyncedFolder!.SiteName)
            .ThenBy(f => f.FileName)
            .Take(1000) // Limit for performance
            .ToListAsync(cancellationToken);

        var resources = files.Select(file => new Resource
        {
            Uri = BuildResourceUri(file),
            Name = file.FileName,
            Description = $"SharePoint: {file.SyncedFolder?.SiteName}/{file.SyncedFolder?.DriveName}/{file.RemotePath}",
            MimeType = file.MimeType ?? "application/octet-stream"
        }).ToList();

        return new ListResourcesResult { Resources = resources };
    }

    /// <summary>
    /// Reads a SharePoint resource by URI.
    /// </summary>
    public async Task<ReadResourceResult> ReadResourceAsync(
        RequestContext<ReadResourceRequestParams> context,
        CancellationToken cancellationToken)
    {
        var uri = context.Params.Uri;
        if (string.IsNullOrEmpty(uri))
        {
            return CreateErrorResult("Resource URI is required.");
        }

        // Parse the URI: nexus://{tenant}/sharepoint/{id or path}
        if (!TryParseResourceUri(uri, out var tenantId, out var resourcePath))
        {
            return CreateErrorResult($"Invalid SharePoint resource URI: {uri}");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Try to find by ID first
        SyncedSharePointFile? file = null;

        if (int.TryParse(resourcePath, out var fileId))
        {
            file = await db.SyncedFiles
                .Include(f => f.SyncedFolder)
                .FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken);
        }

        // Fall back to path search
        file ??= await db.SyncedFiles
            .Include(f => f.SyncedFolder)
            .FirstOrDefaultAsync(f =>
                f.SyncedFolder != null &&
                f.SyncedFolder.TenantId == tenantId &&
                (f.RemotePath.Contains(resourcePath) || f.LocalPath.Contains(resourcePath)),
                cancellationToken);

        if (file is null)
        {
            return CreateErrorResult($"SharePoint resource not found: {uri}");
        }

        if (!File.Exists(file.LocalPath))
        {
            return CreateErrorResult($"Local file not found: {file.LocalPath}. Sync may be required.");
        }

        // Read content
        string content;
        try
        {
            // Return extracted text if available
            if (!string.IsNullOrEmpty(file.ExtractedContent))
            {
                content = file.ExtractedContent;
            }
            else
            {
                content = await File.ReadAllTextAsync(file.LocalPath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cannot read file as text: {Path}", file.LocalPath);
            content = $"[Binary file: {file.FileName}, {file.Size} bytes]";
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = file.MimeType ?? "text/plain",
                    Text = content
                }
            ]
        };
    }

    /// <summary>
    /// Gets the list of resource templates for SharePoint.
    /// </summary>
    public ListResourceTemplatesResult GetResourceTemplates()
    {
        return new ListResourceTemplatesResult
        {
            ResourceTemplates =
            [
                new ResourceTemplate
                {
                    UriTemplate = "nexus://{tenant}/sharepoint/{path}",
                    Name = "SharePoint Document",
                    Description = "Access locally mirrored SharePoint documents. Use * as path wildcard.",
                    MimeType = "text/plain"
                }
            ]
        };
    }

    private static string BuildResourceUri(SyncedSharePointFile file)
    {
        var tenantId = file.SyncedFolder?.TenantId ?? "default";
        return $"{ResourceUriPrefix}{tenantId}{SharePointPath}{file.Id}";
    }

    private static bool TryParseResourceUri(string uri, out string tenantId, out string resourcePath)
    {
        tenantId = string.Empty;
        resourcePath = string.Empty;

        if (!uri.StartsWith(ResourceUriPrefix))
            return false;

        var path = uri[ResourceUriPrefix.Length..];
        var spIndex = path.IndexOf(SharePointPath, StringComparison.OrdinalIgnoreCase);

        if (spIndex < 0)
            return false;

        tenantId = path[..spIndex];
        resourcePath = path[(spIndex + SharePointPath.Length)..];

        return !string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(resourcePath);
    }

    private static ReadResourceResult CreateErrorResult(string message)
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "error",
                    MimeType = "text/plain",
                    Text = message
                }
            ]
        };
    }
}
