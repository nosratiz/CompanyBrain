using CompanyBrain.Dashboard.Features.SharePoint.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp.Resources;

/// <summary>
/// Composite resource handler that combines knowledge, SharePoint, and template resources
/// and applies governance policies (PII masking, system prompting).
/// </summary>
internal static class CompositeResourceHandlers
{
    private const string SharePointUriPrefix = "nexus://";

    public static async ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        // Get knowledge resources
        var knowledgeResult = await KnowledgeResourceHandlers.ListResourcesAsync(request, cancellationToken);

        // Get template resources
        var templateResult = await ResourceTemplateHandlers.ListTemplatesAsResourcesAsync(request, cancellationToken);

        // Get SharePoint resources
        var spHandlers = request.Server.Services?.GetService<SharePointResourceHandlers>();
        var spResult = spHandlers is not null
            ? await spHandlers.ListResourcesAsync(request, cancellationToken)
            : new ListResourcesResult { Resources = [] };

        var combinedResources = new List<Resource>(
            knowledgeResult.Resources.Count + templateResult.Resources.Count + spResult.Resources.Count);
        combinedResources.AddRange(knowledgeResult.Resources);
        combinedResources.AddRange(templateResult.Resources);
        combinedResources.AddRange(spResult.Resources);

        return new ListResourcesResult { Resources = combinedResources };
    }

    public static async ValueTask<ReadResourceResult> ReadResourceAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        var uri = request.Params.Uri;
        ReadResourceResult result;

        // Route to appropriate handler based on URI scheme
        if (uri.StartsWith("templates://", StringComparison.OrdinalIgnoreCase))
        {
            result = await ResourceTemplateHandlers.ReadTemplateResourceAsync(request, cancellationToken);
        }
        else if (uri.StartsWith(SharePointUriPrefix, StringComparison.OrdinalIgnoreCase)
                 && uri.Contains("/sharepoint/", StringComparison.OrdinalIgnoreCase))
        {
            var spHandlers = request.Server.Services?.GetService<SharePointResourceHandlers>();
            result = spHandlers is not null
                ? await spHandlers.ReadResourceAsync(request, cancellationToken)
                : CreateErrorResult($"SharePoint resource handler not available for URI: {uri}");
        }
        else
        {
            // Default to knowledge resources
            result = await KnowledgeResourceHandlers.ReadResourceAsync(request, cancellationToken);
        }

        // Apply governance filter (PII masking and system prompt prefix)
        var governanceFilter = request.Server.Services?.GetService<McpGovernanceFilter>();
        if (governanceFilter is not null)
        {
            result = await governanceFilter.ApplySystemPromptAsync(result, cancellationToken);
        }

        return result;
    }

    private static ReadResourceResult CreateErrorResult(string message) =>
        new()
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "error://",
                    Text = message,
                    MimeType = "text/plain",
                }
            ]
        };
}
