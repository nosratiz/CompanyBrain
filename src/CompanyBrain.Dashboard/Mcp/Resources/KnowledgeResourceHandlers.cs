using CompanyBrain.Application;
using CompanyBrain.Dashboard.Mcp.Collections;
using CompanyBrain.Services;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp.Resources;

internal static class KnowledgeResourceHandlers
{
    private const string IndexResourceUri = "mcp://internal/knowledge/index";
    private const string IndexResourceName = "_index";

    public static async ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        // Track this MCP session so REST API endpoints can broadcast notifications.
        TrackSession(request);

        var collectionManager = GetCollectionManager(request);
        var authorization = GetCollectionAuthorizationHandler(request);
        var collections = await collectionManager.ListCollectionsAsync(cancellationToken);

        var resources = new List<Resource>();
        foreach (var collection in collections)
        {
            var auth = await authorization.AuthorizeCollectionAsync(collection.CollectionId, cancellationToken);
            if (!auth.IsAllowed)
            {
                continue;
            }

            resources.Add(new Resource
            {
                Name = $"collection/{collection.CollectionId}",
                Title = $"📁 {collection.DisplayName}",
                Uri = collection.ResourceUri,
                Description = $"Collection root for {collection.CollectionId}. {collection.DocumentCount} markdown files.",
                MimeType = "text/markdown",
            });

            var files = await collectionManager.ListCollectionDocumentsAsync(collection.CollectionId, cancellationToken);
            foreach (var file in files)
            {
                resources.Add(new Resource
                {
                    Name = $"collection/{collection.CollectionId}/{file}",
                    Title = file,
                    Uri = collectionManager.ToDocumentUri(collection.CollectionId, file),
                    Description = $"Knowledge document in {collection.CollectionId}.",
                    MimeType = "text/markdown",
                });
            }
        }

        // Add index resource at the beginning that lists all available resources
        resources.Insert(0, new Resource
        {
            Name = IndexResourceName,
            Title = "📋 Resource Index",
            Uri = IndexResourceUri,
            Description = $"Dynamic index of all {resources.Count} knowledge resources. Read this to see what's available.",
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
            return await ReadIndexResourceAsync(request, cancellationToken);
        }

        var collectionManager = GetCollectionManager(request);
        var authorization = GetCollectionAuthorizationHandler(request);

        if (collectionManager.TryResolveUri(request.Params.Uri, out var collectionId, out var relativePath))
        {
            var auth = await authorization.AuthorizeCollectionAsync(collectionId, cancellationToken);
            if (!auth.IsAllowed)
            {
                throw new McpException(auth.Reason ?? "Collection is locked by your current license tier.");
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return await ReadCollectionRootAsync(collectionManager, collectionId, cancellationToken);
            }

            return await ReadCollectionDocumentAsync(collectionManager, collectionId, relativePath, cancellationToken);
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
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        var collectionManager = GetCollectionManager(request);
        var authorization = GetCollectionAuthorizationHandler(request);
        var collections = await collectionManager.ListCollectionsAsync(cancellationToken);

        var lines = new List<string>
        {
            "# Knowledge Resource Index",
            string.Empty,
            $"**Total collections discovered:** {collections.Count}",
            string.Empty,
        };

        if (collections.Count == 0)
        {
            lines.Add("_No knowledge resources available yet. Ingest content through the API first._");
        }
        else
        {
            lines.Add("## Available Collections");
            lines.Add(string.Empty);

            foreach (var collection in collections.OrderBy(r => r.CollectionId, StringComparer.OrdinalIgnoreCase))
            {
                var auth = await authorization.AuthorizeCollectionAsync(collection.CollectionId, cancellationToken);
                if (!auth.IsAllowed)
                {
                    continue;
                }

                var files = await collectionManager.ListCollectionDocumentsAsync(collection.CollectionId, cancellationToken);

                lines.Add($"### {collection.DisplayName}");
                lines.Add(string.Empty);
                lines.Add($"- **URI:** `{collection.ResourceUri}`");
                lines.Add($"- **Collection:** {collection.CollectionId}");
                lines.Add($"- **Documents:** {files.Count}");

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

    private static async ValueTask<ReadResourceResult> ReadCollectionRootAsync(
        CollectionManagerService collectionManager,
        string collectionId,
        CancellationToken cancellationToken)
    {
        var files = await collectionManager.ListCollectionDocumentsAsync(collectionId, cancellationToken);
        var lines = new List<string>
        {
            $"# Collection: {collectionId}",
            string.Empty,
            $"**Documents:** {files.Count}",
            string.Empty,
        };

        if (files.Count == 0)
        {
            lines.Add("_No markdown files in this collection yet._");
        }
        else
        {
            foreach (var file in files)
            {
                lines.Add($"- `{collectionManager.ToDocumentUri(collectionId, file)}`");
            }
        }

        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = collectionManager.ToCollectionUri(collectionId),
                    MimeType = "text/markdown",
                    Text = string.Join(Environment.NewLine, lines),
                }
            ],
        };
    }

    private static async ValueTask<ReadResourceResult> ReadCollectionDocumentAsync(
        CollectionManagerService collectionManager,
        string collectionId,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (!collectionManager.TryResolveDocumentPath(collectionId, relativePath, out var fullPath)
            || !File.Exists(fullPath))
        {
            throw new McpException($"Collection resource not found: {collectionId}/{relativePath}");
        }

        var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = collectionManager.ToDocumentUri(collectionId, relativePath),
                    MimeType = "text/markdown",
                    Text = text,
                }
            ],
        };
    }

    private static KnowledgeApplicationService GetKnowledgeApplicationService<TParams>(RequestContext<TParams> request)
        => GetServices(request).GetService(typeof(KnowledgeApplicationService)) as KnowledgeApplicationService
            ?? throw new InvalidOperationException("Knowledge application service was not available.");

    private static CollectionManagerService GetCollectionManager<TParams>(RequestContext<TParams> request)
        => GetServices(request).GetService(typeof(CollectionManagerService)) as CollectionManagerService
            ?? throw new InvalidOperationException("Collection manager service was not available.");

    private static CollectionAuthorizationHandler GetCollectionAuthorizationHandler<TParams>(RequestContext<TParams> request)
        => GetServices(request).GetService(typeof(CollectionAuthorizationHandler)) as CollectionAuthorizationHandler
            ?? throw new InvalidOperationException("Collection authorization handler was not available.");

    private static IServiceProvider GetServices<TParams>(RequestContext<TParams> request)
        => request.Services ?? throw new InvalidOperationException("MCP request services were not available.");

    private static void TrackSession<TParams>(RequestContext<TParams> request)
    {
        var tracker = GetServices(request).GetService(typeof(McpSessionTracker)) as McpSessionTracker;
        tracker?.Track(request.Server);
    }
}
