using CompanyBrain.Api.Contracts;
using CompanyBrain.Api.ResultMapping;
using CompanyBrain.Api.Validation;
using CompanyBrain.Application;
using CompanyBrain.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Api;

internal static class CompanyBrainApi
{
    public static IEndpointRouteBuilder MapCompanyBrainApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/knowledge")
            .WithTags("Knowledge");

        group.MapPost("/wiki", IngestWikiAsync)
            .WithName("IngestWiki")
            .Produces<IngestResultResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .DisableAntiforgery();

        group.MapPost("/documents/path", IngestDocumentPathAsync)
            .WithName("IngestDocumentFromPath")
            .Produces<IngestResultResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .DisableAntiforgery();

        group.MapPost("/documents/upload", IngestDocumentUploadAsync)
            .WithName("IngestDocumentUpload")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<IngestResultResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .DisableAntiforgery();

        group.MapGet("/search", SearchAsync)
            .WithName("SearchKnowledge")
            .Produces<SearchResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/resources", ListResourcesAsync)
            .WithName("ListKnowledgeResources")
            .Produces<IReadOnlyList<KnowledgeResourceDescriptor>>(StatusCodes.Status200OK);

        group.MapGet("/resources/{fileName}", GetResourceAsync)
            .WithName("GetKnowledgeResource")
            .Produces<KnowledgeResourceContent>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> IngestWikiAsync(
        IngestWikiRequest request,
        [FromServices] IValidator<IngestWikiRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await service.IngestWikiAsync(request.Url, request.Name, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        var document = result.Value;
        return TypedResults.Ok(new IngestResultResponse(document.FileName, document.ResourceUri, document.Existed));
    }

    private static async Task<IResult> IngestDocumentPathAsync(
        IngestPathRequest request,
        [FromServices] IValidator<IngestPathRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await service.IngestDocumentFromPathAsync(request.LocalPath, request.Name, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        var document = result.Value;
        return TypedResults.Ok(new IngestResultResponse(document.FileName, document.ResourceUri, document.Existed));
    }

    private static async Task<IResult> IngestDocumentUploadAsync(
        HttpRequest request,
        [FromServices] IValidator<UploadDocumentRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files["file"];
        var name = form["name"].FirstOrDefault();
        var uploadRequest = new UploadDocumentRequest(file, name);

        var validation = await validator.ValidateAsync(uploadRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        await using var stream = file!.OpenReadStream();
        var result = await service.IngestUploadedDocumentAsync(stream, file.FileName, name, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        var document = result.Value;
        return TypedResults.Ok(new IngestResultResponse(document.FileName, document.ResourceUri, document.Existed));
    }

    private static async Task<IResult> SearchAsync(
        string query,
        int? maxResults,
        [FromServices] IValidator<SearchRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        CancellationToken cancellationToken)
    {
        var request = new SearchRequest(query, maxResults);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var effectiveMaxResults = request.MaxResults ?? 5;
        var result = await service.SearchAsync(request.Query, effectiveMaxResults, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        return TypedResults.Ok(new SearchResponse(request.Query, effectiveMaxResults, result.Value));
    }

    private static async Task<IResult> ListResourcesAsync(
        [FromServices] KnowledgeApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListResourcesAsync(cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        return TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> GetResourceAsync(
        string fileName,
        [FromServices] KnowledgeApplicationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetResourceAsync(fileName, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        return TypedResults.Ok(result.Value);
    }
}