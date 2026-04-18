using CompanyBrain.Application;
using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Api.ResultMapping;
using CompanyBrain.Dashboard.Api.Validation;
using CompanyBrain.Dashboard.Mcp;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Dashboard.Api;

internal static class ResourceTemplateApi
{
    public static IEndpointRouteBuilder MapResourceTemplateApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/templates")
            .WithTags("Resource Templates");

        group.MapPost("/clone", CloneRepositoryAsync)
            .WithName("CloneGitRepository")
            .WithDescription("Clone a git repository as a resource template.")
            .Produces<CloneGitRepositoryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .DisableAntiforgery();

        group.MapGet("/", ListTemplatesAsync)
            .WithName("ListResourceTemplates")
            .WithDescription("List all cloned resource templates.")
            .Produces<IReadOnlyList<ResourceTemplateResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{templateName}/files/{**relativePath}", GetTemplateFileAsync)
            .WithName("GetTemplateFile")
            .WithDescription("Get the content of a specific file from a template.")
            .Produces<string>(StatusCodes.Status200OK, "text/plain")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{templateName}", DeleteTemplateAsync)
            .WithName("DeleteResourceTemplate")
            .WithDescription("Delete a resource template.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CloneRepositoryAsync(
        CloneGitRepositoryRequest request,
        [FromServices] IValidator<CloneGitRepositoryRequest> validator,
        [FromServices] ResourceTemplateApplicationService service,
        [FromServices] McpSessionTracker sessionTracker,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await service.CloneRepositoryAsync(
            request.RepositoryUrl,
            request.TemplateName,
            request.Branch,
            cancellationToken);

        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        await sessionTracker.NotifyResourceListChangedAsync(cancellationToken);

        var cloned = result.Value;
        return TypedResults.Ok(new CloneGitRepositoryResponse(
            cloned.TemplateName,
            cloned.RepositoryUrl,
            cloned.LocalPath,
            cloned.Branch,
            cloned.FileCount,
            cloned.AlreadyExisted));
    }

    private static async Task<IResult> ListTemplatesAsync(
        [FromServices] ResourceTemplateApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListTemplatesAsync(cancellationToken);

        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        var templates = result.Value.Select(t => new ResourceTemplateResponse(
            t.Name,
            t.RepositoryUrl,
            t.LocalPath,
            t.Branch,
            t.ClonedAt,
            t.Files.Count,
            t.Files)).ToList();

        return TypedResults.Ok<IReadOnlyList<ResourceTemplateResponse>>(templates);
    }

    private static async Task<IResult> GetTemplateFileAsync(
        string templateName,
        string relativePath,
        [FromServices] ResourceTemplateApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetTemplateFileContentAsync(templateName, relativePath, cancellationToken);

        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        return TypedResults.Text(result.Value, "text/plain");
    }

    private static IResult DeleteTemplateAsync(
        string templateName,
        [FromServices] ResourceTemplateApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = service.DeleteTemplate(templateName, cancellationToken);

        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        return TypedResults.NoContent();
    }
}
