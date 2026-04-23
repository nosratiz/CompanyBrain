using System.ComponentModel;
using CompanyBrain.Application;
using CompanyBrain.Models;
using FluentResults;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp.Tools;

[McpServerToolType]
internal sealed class CompanyBrainTools(
    KnowledgeApplicationService service,
    GovernanceToolWrapper governance)
{
    [McpServerTool, Description("Lists the knowledge documents that were already saved by the API and are available for MCP consumption.")]
    public async Task<string> ListResources(CancellationToken cancellationToken)
    {
        var result = await service.ListResourcesAsync(cancellationToken);
        var resources = EnsureSuccess(result);

        if (resources.Count == 0)
        {
            return "No saved knowledge resources are currently available. Ingest content through the API first.";
        }

        return FormatResourceList(resources);
    }

    [McpServerTool, Description("Searches the stored knowledge base. Documents are assumed to have been ingested and saved by the API already.")]
    public async Task<string> SearchDocs(
        [Description("Search query.")] string query,
        [Description("Maximum number of results to include. Defaults to 5.")] int maxResults,
        [Description("Optional named collection scope (e.g. Engineering, HR).")]
        string? collectionId,
        CancellationToken cancellationToken)
    {
        var effectiveMaxResults = maxResults <= 0 ? 5 : maxResults;
        var result = string.IsNullOrWhiteSpace(collectionId)
            ? await service.SearchAsync(query, effectiveMaxResults, cancellationToken)
            : await service.SearchCollectionAsync(collectionId, query, effectiveMaxResults, cancellationToken);

        var text = EnsureSuccess(result);
        return await governance.PruneTextAsync(text, query, cancellationToken);
    }

    [McpServerTool, Description("Ingests a wiki page from a URL and saves it as a knowledge resource. After ingestion the MCP resource list is updated automatically.")]
    public async Task<string> IngestWikiDocument(
        McpServer server,
        [Description("The absolute HTTP(S) URL of the wiki page to ingest.")] string url,
        [Description("A short logical name for the document.")] string name,
        CancellationToken cancellationToken)
    {
        var result = await service.IngestWikiAsync(url, name, cancellationToken);
        var document = EnsureSuccess(result);

        await NotifyResourceListChangedAsync(server, cancellationToken);

        return $"Ingested '{document.FileName}' (uri: {document.ResourceUri}, existed: {document.Existed}). The resource list has been updated.";
    }

    [McpServerTool, Description("Ingests a local document from a file path and saves it as a knowledge resource. Supports .md, .txt, .html, .htm, .docx, .doc, .pdf, and .rtf files. After ingestion the MCP resource list is updated automatically.")]
    public async Task<string> IngestDocumentFromPath(
        McpServer server,
        [Description("The local file path of the document to ingest.")] string localPath,
        [Description("Optional logical name for the document. If not provided, the file name is used.")] string? name,
        CancellationToken cancellationToken)
    {
        var result = await service.IngestDocumentFromPathAsync(localPath, name, cancellationToken);
        var document = EnsureSuccess(result);

        await NotifyResourceListChangedAsync(server, cancellationToken);

        return $"Ingested '{document.FileName}' (uri: {document.ResourceUri}, existed: {document.Existed}). The resource list has been updated.";
    }

    private static async Task NotifyResourceListChangedAsync(McpServer server, CancellationToken cancellationToken)
    {
        try
        {
            await server.SendNotificationAsync(
                NotificationMethods.ResourceListChangedNotification,
                cancellationToken);
        }
        catch
        {
            // Best-effort: the client may not support this notification.
        }
    }

    private static string FormatResourceList(IReadOnlyList<KnowledgeResourceDescriptor> resources)
    {
        var lines = new List<string>
        {
            "Available knowledge resources:",
            string.Empty,
        };

        foreach (var resource in resources.OrderBy(resource => resource.Name, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {resource.Title ?? resource.Name}");
            lines.Add($"  Name: {resource.Name}");
            lines.Add($"  Uri: {resource.Uri}");

            if (!string.IsNullOrWhiteSpace(resource.Description))
            {
                lines.Add($"  Description: {resource.Description}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static T EnsureSuccess<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }
}
