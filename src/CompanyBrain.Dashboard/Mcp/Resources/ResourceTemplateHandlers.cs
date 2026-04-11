using CompanyBrain.Application;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp.Resources;

internal static class ResourceTemplateHandlers
{
    private const string TemplateIndexUri = "templates://index";
    private const string TemplateUriPrefix = "templates://";

    public static async ValueTask<ListResourcesResult> ListTemplatesAsResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        var service = GetResourceTemplateService(request);
        var result = await service.ListTemplatesAsync(cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        var resources = new List<Resource>
        {
            // Index resource that lists all templates
            new Resource
            {
                Name = "_templates_index",
                Title = "📁 Resource Templates Index",
                Uri = TemplateIndexUri,
                Description = $"Index of {result.Value.Count} cloned git repository templates. Read this to see available code templates.",
                MimeType = "text/markdown",
            },
        };

        // Add each template as a resource
        foreach (var template in result.Value)
        {
            resources.Add(new Resource
            {
                Name = $"template_{template.Name}",
                Title = $"📦 {template.Name}",
                Uri = $"{TemplateUriPrefix}{template.Name}",
                Description = $"Repository: {template.RepositoryUrl} | Branch: {template.Branch} | Files: {template.Files.Count}",
                MimeType = "text/markdown",
            });
        }

        return new ListResourcesResult { Resources = resources };
    }

    public static async ValueTask<ReadResourceResult> ReadTemplateResourceAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        var service = GetResourceTemplateService(request);
        var uri = request.Params.Uri;

        // Handle index resource
        if (uri == TemplateIndexUri)
        {
            return await ReadTemplateIndexAsync(service, cancellationToken);
        }

        // Handle individual template resource
        if (uri.StartsWith(TemplateUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var templateName = uri[TemplateUriPrefix.Length..];

            // Check if it's a file path within the template
            var slashIndex = templateName.IndexOf('/');
            if (slashIndex > 0)
            {
                var name = templateName[..slashIndex];
                var filePath = templateName[(slashIndex + 1)..];
                return await ReadTemplateFileAsync(service, name, filePath, cancellationToken);
            }

            // It's the template root - return the file listing
            return await ReadTemplateAsync(service, templateName, cancellationToken);
        }

        throw new McpException($"Unknown resource URI: {uri}");
    }

    private static async ValueTask<ReadResourceResult> ReadTemplateIndexAsync(
        ResourceTemplateApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListTemplatesAsync(cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        var lines = new List<string>
        {
            "# Resource Templates Index",
            string.Empty,
            $"**Total templates:** {result.Value.Count}",
            string.Empty,
        };

        if (result.Value.Count == 0)
        {
            lines.Add("_No resource templates available. Clone a git repository through the Import page or use the `CloneGitRepository` tool._");
        }
        else
        {
            lines.Add("## Available Templates");
            lines.Add(string.Empty);

            foreach (var template in result.Value.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"### {template.Name}");
                lines.Add(string.Empty);
                lines.Add($"- **Repository:** {template.RepositoryUrl}");
                lines.Add($"- **Branch:** {template.Branch}");
                lines.Add($"- **Files:** {template.Files.Count}");
                lines.Add($"- **Cloned:** {template.ClonedAt:u}");
                lines.Add($"- **URI:** `templates://{template.Name}`");
                lines.Add(string.Empty);

                if (template.Files.Count > 0 && template.Files.Count <= 20)
                {
                    lines.Add("**Files:**");
                    foreach (var file in template.Files.Take(20))
                    {
                        lines.Add($"  - `{file}`");
                    }

                    if (template.Files.Count > 20)
                    {
                        lines.Add($"  - ... and {template.Files.Count - 20} more files");
                    }

                    lines.Add(string.Empty);
                }
            }
        }

        return new ReadResourceResult
        {
            Contents = new List<ResourceContents>
            {
                new TextResourceContents
                {
                    Uri = TemplateIndexUri,
                    MimeType = "text/markdown",
                    Text = string.Join(Environment.NewLine, lines),
                },
            },
        };
    }

    private static async ValueTask<ReadResourceResult> ReadTemplateAsync(
        ResourceTemplateApplicationService service,
        string templateName,
        CancellationToken cancellationToken)
    {
        var result = await service.ListTemplatesAsync(cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        var template = result.Value.FirstOrDefault(t =>
            t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

        if (template is null)
        {
            throw new McpException($"Template not found: {templateName}");
        }

        var lines = new List<string>
        {
            $"# Template: {template.Name}",
            string.Empty,
            $"**Repository:** {template.RepositoryUrl}",
            $"**Branch:** {template.Branch}",
            $"**Cloned:** {template.ClonedAt:u}",
            $"**Files:** {template.Files.Count}",
            string.Empty,
            "## File Listing",
            string.Empty,
            "To read a specific file, use the URI format: `templates://{templateName}/{filePath}`",
            string.Empty,
        };

        foreach (var file in template.Files)
        {
            lines.Add($"- `templates://{template.Name}/{file}`");
        }

        return new ReadResourceResult
        {
            Contents = new List<ResourceContents>
            {
                new TextResourceContents
                {
                    Uri = $"{TemplateUriPrefix}{templateName}",
                    MimeType = "text/markdown",
                    Text = string.Join(Environment.NewLine, lines),
                },
            },
        };
    }

    private static async ValueTask<ReadResourceResult> ReadTemplateFileAsync(
        ResourceTemplateApplicationService service,
        string templateName,
        string filePath,
        CancellationToken cancellationToken)
    {
        var result = await service.GetTemplateFileContentAsync(templateName, filePath, cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        }

        var mimeType = GetMimeType(filePath);

        return new ReadResourceResult
        {
            Contents = new List<ResourceContents>
            {
                new TextResourceContents
                {
                    Uri = $"{TemplateUriPrefix}{templateName}/{filePath}",
                    MimeType = mimeType,
                    Text = result.Value,
                },
            },
        };
    }

    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".cs" => "text/x-csharp",
            ".js" => "text/javascript",
            ".ts" => "text/typescript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".yaml" or ".yml" => "text/yaml",
            ".md" => "text/markdown",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".py" => "text/x-python",
            ".go" => "text/x-go",
            ".rs" => "text/x-rust",
            ".java" => "text/x-java",
            ".razor" => "text/x-razor",
            ".csproj" or ".fsproj" or ".vbproj" => "application/xml",
            _ => "text/plain",
        };
    }

    private static ResourceTemplateApplicationService GetResourceTemplateService<TParams>(RequestContext<TParams> request)
        where TParams : class
    {
        return request.Server.Services?.GetService(typeof(ResourceTemplateApplicationService)) as ResourceTemplateApplicationService
            ?? throw new McpException("ResourceTemplateApplicationService not available.");
    }
}
