using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp.Resources;

/// <summary>
/// Composite resource handler that combines knowledge and template resources
/// and applies governance policies (PII masking, system prompting).
/// </summary>
internal static class CompositeResourceHandlers
{
    public static async ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        // Get knowledge resources
        var knowledgeResult = await KnowledgeResourceHandlers.ListResourcesAsync(request, cancellationToken);

        // Get template resources
        var templateResult = await ResourceTemplateHandlers.ListTemplatesAsResourcesAsync(request, cancellationToken);

        // Combine both resource lists
        var combinedResources = new List<Resource>(knowledgeResult.Resources);
        combinedResources.AddRange(templateResult.Resources);

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
}
