using System.ComponentModel;
using CompanyBrain.Application;
using FluentResults;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResourceTemplateModel = CompanyBrain.Models.ResourceTemplate;

namespace CompanyBrain.Dashboard.Mcp.Tools;

[McpServerToolType]
internal sealed class ResourceTemplateTools(ResourceTemplateApplicationService service)
{
    [McpServerTool, Description("Lists all available resource templates cloned from git repositories. Returns template names, repository URLs, branches, and file counts.")]
    public async Task<string> ListResourceTemplates(CancellationToken cancellationToken)
    {
        var result = await service.ListTemplatesAsync(cancellationToken);
        var templates = EnsureSuccess(result);

        if (templates.Count == 0)
        {
            return "No resource templates available. Use CloneGitRepository to clone a template repository.";
        }

        return FormatTemplateList(templates);
    }

    [McpServerTool, Description("Clones a git repository as a resource template. The template becomes available as an MCP resource for code reference and generation.")]
    public async Task<string> CloneGitRepository(
        McpServer server,
        [Description("The git repository URL (HTTPS, SSH, or git://).")] string repositoryUrl,
        [Description("A unique name for this template. Use lowercase letters, numbers, and hyphens.")] string templateName,
        [Description("The branch to clone. Leave empty for the default branch.")] string? branch,
        CancellationToken cancellationToken)
    {
        var result = await service.CloneRepositoryAsync(repositoryUrl, templateName, branch, cancellationToken);
        var cloned = EnsureSuccess(result);

        await NotifyResourceListChangedAsync(server, cancellationToken);

        var status = cloned.AlreadyExisted ? "Updated existing" : "Cloned new";
        return $"{status} template '{cloned.TemplateName}' from {cloned.RepositoryUrl} (branch: {cloned.Branch}, files: {cloned.FileCount}). " +
               $"The template is now available as MCP resource: templates://{cloned.TemplateName}";
    }

    [McpServerTool, Description("Reads the content of a specific file from a resource template.")]
    public async Task<string> ReadTemplateFile(
        [Description("The name of the template.")] string templateName,
        [Description("The relative path to the file within the template.")] string filePath,
        CancellationToken cancellationToken)
    {
        var result = await service.GetTemplateFileContentAsync(templateName, filePath, cancellationToken);
        return EnsureSuccess(result);
    }

    [McpServerTool, Description("Gets the file listing for a resource template.")]
    public async Task<string> GetTemplateFiles(
        [Description("The name of the template.")] string templateName,
        CancellationToken cancellationToken)
    {
        var result = await service.ListTemplatesAsync(cancellationToken);
        var templates = EnsureSuccess(result);

        var template = templates.FirstOrDefault(t =>
            t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

        if (template is null)
        {
            throw new McpException($"Template not found: {templateName}");
        }

        var lines = new List<string>
        {
            $"# Files in template: {template.Name}",
            string.Empty,
            $"Repository: {template.RepositoryUrl}",
            $"Branch: {template.Branch}",
            $"Total files: {template.Files.Count}",
            string.Empty,
        };

        foreach (var file in template.Files)
        {
            lines.Add($"- {file}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    [McpServerTool, Description("Deletes a resource template from the system.")]
    public async Task<string> DeleteResourceTemplate(
        McpServer server,
        [Description("The name of the template to delete.")] string templateName,
        CancellationToken cancellationToken)
    {
        var result = service.DeleteTemplate(templateName, cancellationToken);

        if (result.IsFailed)
        {
            throw new McpException(string.Join(Environment.NewLine, result.Errors.Select(e => e.Message)));
        }

        await NotifyResourceListChangedAsync(server, cancellationToken);

        return $"Successfully deleted template '{templateName}'. The MCP resource list has been updated.";
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

    private static string FormatTemplateList(IReadOnlyList<ResourceTemplateModel> templates)
    {
        var lines = new List<string>
        {
            "Available resource templates:",
            string.Empty,
        };

        foreach (var template in templates.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"- {template.Name}");
            lines.Add($"  Repository: {template.RepositoryUrl}");
            lines.Add($"  Branch: {template.Branch}");
            lines.Add($"  Files: {template.Files.Count}");
            lines.Add($"  MCP URI: templates://{template.Name}");
            lines.Add(string.Empty);
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
