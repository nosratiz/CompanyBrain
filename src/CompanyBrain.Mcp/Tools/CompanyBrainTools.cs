using System.ComponentModel;
using CompanyBrain.Application;
using CompanyBrain.Models;
using FluentResults;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Mcp.Tools;

[McpServerToolType]
internal sealed class CompanyBrainTools(KnowledgeApplicationService service)
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
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(query, maxResults <= 0 ? 5 : maxResults, cancellationToken);
        return EnsureSuccess(result);
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