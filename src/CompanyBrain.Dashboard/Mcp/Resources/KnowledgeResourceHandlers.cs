using CompanyBrain.Application;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp.Resources;

internal static class KnowledgeResourceHandlers
{
    private const string IndexResourceUri = "knowledge://index";
    private const string IndexResourceName = "_index";

    public static async ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        // Track this MCP session so REST API endpoints can broadcast notifications.
        TrackSession(request);

        var service = GetKnowledgeApplicationService(request);
        var result = await service.ListResourcesAsync(cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        var resources = result.Value
            .Select(resource => new Resource
            {
                Name = resource.Name,
                Title = resource.Title,
                Uri = resource.Uri,
                Description = resource.Description,
                MimeType = resource.MimeType,
                Size = resource.Size,
            })
            .ToList();

        // Add index resource at the beginning that lists all available resources
        resources.Insert(0, new Resource
        {
            Name = IndexResourceName,
            Title = "📋 Resource Index",
            Uri = IndexResourceUri,
            Description = $"Dynamic index of all {result.Value.Count} knowledge resources. Read this to see what's available.",
            MimeType = "text/markdown",
        });

        return new ListResourcesResult { Resources = resources };
    }

    public static async ValueTask<ReadResourceResult> ReadResourceAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        var service = GetKnowledgeApplicationService(request);

        // Handle special index resource
        if (request.Params.Uri == IndexResourceUri)
        {
            return await ReadIndexResourceAsync(service, cancellationToken);
        }

        var result = await service.ReadResourceAsync(request.Params.Uri, cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        return new ReadResourceResult
        {
            Contents = new List<ResourceContents>
            {
                new TextResourceContents
                {
                    Uri = result.Value.Uri,
                    MimeType = result.Value.MimeType,
                    Text = result.Value.Content,
                },
            },
        };
    }

    private static async ValueTask<ReadResourceResult> ReadIndexResourceAsync(
        KnowledgeApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListResourcesAsync(cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        var resources = result.Value;
        var lines = new List<string>
        {
            "# Knowledge Resource Index",
            string.Empty,
            $"**Total resources:** {resources.Count}",
            string.Empty,
        };

        if (resources.Count == 0)
        {
            lines.Add("_No knowledge resources available yet. Ingest content through the API first._");
        }
        else
        {
            lines.Add("## Available Resources");
            lines.Add(string.Empty);

            foreach (var resource in resources.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"### {resource.Title ?? resource.Name}");
                lines.Add(string.Empty);
                lines.Add($"- **URI:** `{resource.Uri}`");
                lines.Add($"- **Name:** {resource.Name}");

                if (!string.IsNullOrWhiteSpace(resource.Description))
                {
                    lines.Add($"- **Description:** {resource.Description}");
                }

                if (resource.Size.HasValue)
                {
                    lines.Add($"- **Size:** {resource.Size.Value:N0} bytes");
                }

                lines.Add(string.Empty);
            }
        }

        return new ReadResourceResult
        {
            Contents = new List<ResourceContents>
            {
                new TextResourceContents
                {
                    Uri = IndexResourceUri,
                    MimeType = "text/markdown",
                    Text = string.Join(Environment.NewLine, lines),
                },
            },
        };
    }

    private static KnowledgeApplicationService GetKnowledgeApplicationService<TParams>(RequestContext<TParams> request)
        => GetServices(request).GetService(typeof(KnowledgeApplicationService)) as KnowledgeApplicationService
            ?? throw new InvalidOperationException("Knowledge application service was not available.");

    private static IServiceProvider GetServices<TParams>(RequestContext<TParams> request)
        => request.Services ?? throw new InvalidOperationException("MCP request services were not available.");

    private static void TrackSession<TParams>(RequestContext<TParams> request)
    {
        var tracker = GetServices(request).GetService(typeof(McpSessionTracker)) as McpSessionTracker;
        tracker?.Track(request.Server);
    }
}
