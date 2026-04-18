using CompanyBrain.Models;
using CompanyBrain.Services;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Application;

/// <summary>
/// Application service for managing resource templates cloned from git repositories.
/// </summary>
public sealed class ResourceTemplateApplicationService
{
    private readonly GitRepositoryService gitRepositoryService;
    private readonly ILogger<ResourceTemplateApplicationService> logger;

    public ResourceTemplateApplicationService(
        GitRepositoryService gitRepositoryService,
        ILogger<ResourceTemplateApplicationService>? logger = null)
    {
        this.gitRepositoryService = gitRepositoryService;
        this.logger = logger ?? NullLogger<ResourceTemplateApplicationService>.Instance;
    }

    public async Task<Result<ClonedRepositoryResult>> CloneRepositoryAsync(
        string repositoryUrl,
        string templateName,
        string? branch,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Cloning repository '{RepositoryUrl}' as template '{TemplateName}' (branch: {Branch}).",
            repositoryUrl,
            templateName,
            branch ?? "default");

        return await gitRepositoryService.CloneRepositoryAsync(
            repositoryUrl,
            templateName,
            branch,
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<ResourceTemplate>>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Listing resource templates.");
        var templates = await gitRepositoryService.ListTemplatesAsync(cancellationToken);
        return Result.Ok(templates);
    }

    public async Task<Result<string>> GetTemplateFileContentAsync(
        string templateName,
        string relativePath,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Reading file '{RelativePath}' from template '{TemplateName}'.", relativePath, templateName);
        return await gitRepositoryService.GetTemplateFileContentAsync(templateName, relativePath, cancellationToken);
    }

    public Result DeleteTemplate(string templateName, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleting template '{TemplateName}'.", templateName);
        return gitRepositoryService.DeleteTemplate(templateName, cancellationToken);
    }
}
