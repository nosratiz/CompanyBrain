using CompanyBrain.Application;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Resources;

internal static class KnowledgeResourceHandlers
{
    public static async ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        var service = GetKnowledgeApplicationService(request);
        var result = await service.ListResourcesAsync(cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        return new ListResourcesResult
        {
            Resources = result.Value
                .Select(resource => new Resource
                {
                    Name = resource.Name,
                    Title = resource.Title,
                    Uri = resource.Uri,
                    Description = resource.Description,
                    MimeType = resource.MimeType,
                    Size = resource.Size,
                })
                .ToList(),
        };
    }

    public static async ValueTask<ReadResourceResult> ReadResourceAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        var service = GetKnowledgeApplicationService(request);
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

    private static KnowledgeApplicationService GetKnowledgeApplicationService<TParams>(RequestContext<TParams> request)
        => GetServices(request).GetService(typeof(KnowledgeApplicationService)) as KnowledgeApplicationService
            ?? throw new InvalidOperationException("Knowledge application service was not available.");

    private static IServiceProvider GetServices<TParams>(RequestContext<TParams> request)
        => request.Services ?? throw new InvalidOperationException("MCP request services were not available.");
}