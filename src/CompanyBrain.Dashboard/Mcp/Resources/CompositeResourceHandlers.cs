using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp.Resources;

/// <summary>
/// Composite resource handler that combines knowledge and template resources.
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

        // Route to appropriate handler based on URI scheme
        if (uri.StartsWith("templates://", StringComparison.OrdinalIgnoreCase))
        {
            return await ResourceTemplateHandlers.ReadTemplateResourceAsync(request, cancellationToken);
        }

        // Default to knowledge resources
        return await KnowledgeResourceHandlers.ReadResourceAsync(request, cancellationToken);
    }
}
