using CompanyBrain.Dashboard.Api.Contracts;
using CompanyBrain.Dashboard.Api.ResultMapping;
using CompanyBrain.Dashboard.Api.Validation;
using CompanyBrain.Dashboard.Data.Audit;
using CompanyBrain.Dashboard.Mcp;
using CompanyBrain.Dashboard.Services.Audit;
using CompanyBrain.Application;
using CompanyBrain.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CompanyBrain.Dashboard.Api;

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

        group.MapPost("/wiki/batch", IngestWikiBatchAsync)
            .WithName("IngestWikiBatch")
            .WithDescription("Discover all wiki links from a URL and ingest each one as a separate document.")
            .Produces<IngestWikiBatchResponse>()
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

        group.MapPost("/database-schema", IngestDatabaseSchemaAsync)
            .WithName("IngestDatabaseSchema")
            .WithDescription("Read a SQL Server database schema and save it as a knowledge document.")
            .Produces<IngestResultResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .DisableAntiforgery();

        group.MapGet("/search", SearchAsync)
            .WithName("SearchKnowledge")
            .Produces<SearchResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/resources", ListResourcesAsync)
            .WithName("ListKnowledgeResources")
            .Produces<IReadOnlyList<KnowledgeResourceDescriptor>>(StatusCodes.Status200OK);

        group.MapGet("/resources/{**fileName}", GetResourceAsync)
            .WithName("GetKnowledgeResource")
            .Produces<KnowledgeResourceContent>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/resources/{**fileName}", DeleteResourceAsync)
            .WithName("DeleteKnowledgeResource")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> IngestWikiAsync(
        IngestWikiRequest request,
        [FromServices] IValidator<IngestWikiRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        [FromServices] McpSessionTracker sessionTracker,
        [FromServices] IAuditService audit,
        HttpContext httpContext,
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

        await sessionTracker.NotifyResourceListChangedAsync(cancellationToken);

        var document = result.Value;
        _ = audit.LogAsync(AuditEventType.DocumentCreated, new AuditEntry(
            ResourceType: "Document",
            ResourceId: document.FileName,
            ResourceName: document.FileName,
            Metadata: new { source = "wiki", url = request.Url },
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString()));

        return TypedResults.Ok(new IngestResultResponse(document.FileName, document.ResourceUri, document.Existed));
    }

    private static async Task<IResult> IngestWikiBatchAsync(
        IngestWikiBatchRequest request,
        [FromServices] IValidator<IngestWikiBatchRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        [FromServices] McpSessionTracker sessionTracker,
        [FromServices] IAuditService audit,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await service.IngestWikiBatchAsync(request.Url, request.LinkSelector, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        await sessionTracker.NotifyResourceListChangedAsync(cancellationToken);

        var batchResult = result.Value;
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        _ = audit.LogAsync(AuditEventType.DocumentCreated, new AuditEntry(
            ResourceType: "Document",
            ResourceName: request.Url,
            Metadata: new { source = "wiki-batch", url = request.Url, ingested = batchResult.SuccessfullyIngested, failed = batchResult.Failed },
            IpAddress: ip));

        var responseItems = batchResult.Results.Select(r =>
            new IngestWikiBatchItemResult(r.Url, r.Name, r.FileName, r.ResourceUri, r.Success, r.Error)).ToList();

        return TypedResults.Ok(new IngestWikiBatchResponse(
            batchResult.TotalDiscovered,
            batchResult.SuccessfullyIngested,
            batchResult.Failed,
            responseItems));
    }

    private static async Task<IResult> IngestDocumentPathAsync(
        IngestPathRequest request,
        [FromServices] IValidator<IngestPathRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        [FromServices] McpSessionTracker sessionTracker,
        [FromServices] IAuditService audit,
        HttpContext httpContext,
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

        await sessionTracker.NotifyResourceListChangedAsync(cancellationToken);

        var document = result.Value;
        _ = audit.LogAsync(AuditEventType.DocumentCreated, new AuditEntry(
            ResourceType: "Document",
            ResourceId: document.FileName,
            ResourceName: document.FileName,
            Metadata: new { source = "path" },
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString()));

        return TypedResults.Ok(new IngestResultResponse(document.FileName, document.ResourceUri, document.Existed));
    }

    private static async Task<IResult> IngestDocumentUploadAsync(
        HttpRequest request,
        [FromServices] IValidator<UploadDocumentRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        [FromServices] McpSessionTracker sessionTracker,
        [FromServices] IAuditService audit,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files["file"];
        var name = form["name"].FirstOrDefault();
        var collectionId = form["collectionId"].FirstOrDefault();
        var uploadRequest = new UploadDocumentRequest(file, name);

        var validation = await validator.ValidateAsync(uploadRequest, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        await using var stream = file!.OpenReadStream();
        var result = string.IsNullOrWhiteSpace(collectionId)
            ? await service.IngestUploadedDocumentAsync(stream, file.FileName, name, cancellationToken)
            : await service.IngestUploadedDocumentIntoCollectionAsync(collectionId, stream, file.FileName, name, cancellationToken);

        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        await sessionTracker.NotifyResourceListChangedAsync(cancellationToken);

        var document = result.Value;
        _ = audit.LogAsync(AuditEventType.DocumentCreated, new AuditEntry(
            ActorEmail: request.Headers["X-Actor-Email"].FirstOrDefault(),
            ResourceType: "Document",
            ResourceId: document.FileName,
            ResourceName: document.FileName,
            Metadata: new { source = "upload", originalFileName = file.FileName, collectionId },
            IpAddress: request.HttpContext.Connection.RemoteIpAddress?.ToString()));

        return TypedResults.Ok(new IngestResultResponse(document.FileName, document.ResourceUri, document.Existed));
    }

    private static async Task<IResult> SearchAsync(
        string query,
        int? maxResults,
        string? collectionId,
        [FromServices] IValidator<SearchRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        [FromServices] IAuditService audit,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = new SearchRequest(query, maxResults, collectionId);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var effectiveMaxResults = request.MaxResults ?? 5;
        var result = string.IsNullOrWhiteSpace(request.CollectionId)
            ? await service.SearchAsync(request.Query, effectiveMaxResults, cancellationToken)
            : await service.SearchCollectionAsync(request.CollectionId, request.Query, effectiveMaxResults, cancellationToken);

        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        _ = audit.LogAsync(AuditEventType.SearchPerformed, new AuditEntry(
            ResourceType: "KnowledgeBase",
            ResourceId: request.CollectionId,
            Metadata: new { query = request.Query, maxResults = effectiveMaxResults, collectionId = request.CollectionId, resultLength = result.Value.Length },
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString()));

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

    private static async Task<IResult> DeleteResourceAsync(
        string fileName,
        [FromServices] KnowledgeApplicationService service,
        [FromServices] McpSessionTracker sessionTracker,
        [FromServices] IAuditService audit,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = service.DeleteResourceAsync(fileName, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        await sessionTracker.NotifyResourceListChangedAsync(cancellationToken);

        _ = audit.LogAsync(AuditEventType.DocumentDeleted, new AuditEntry(
            ResourceType: "Document",
            ResourceId: fileName,
            ResourceName: fileName,
            Metadata: new { deletedBy = "api" },
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString()));

        return TypedResults.NoContent();
    }

    private static async Task<IResult> IngestDatabaseSchemaAsync(
        IngestDatabaseSchemaRequest request,
        [FromServices] IValidator<IngestDatabaseSchemaRequest> validator,
        [FromServices] KnowledgeApplicationService service,
        [FromServices] McpSessionTracker sessionTracker,
        [FromServices] IAuditService audit,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        if (!Enum.TryParse<CompanyBrain.Models.DatabaseProvider>(request.Provider, ignoreCase: true, out var provider))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Provider"] = ["Provider must be SqlServer, PostgreSql, or MySql."]
            });
        }

        var result = await service.IngestDatabaseSchemaAsync(request.ConnectionString, request.Name, provider, cancellationToken);
        if (result.IsFailed)
        {
            return result.ToProblemResult();
        }

        await sessionTracker.NotifyResourceListChangedAsync(cancellationToken);

        var document = result.Value;
        _ = audit.LogAsync(AuditEventType.DocumentCreated, new AuditEntry(
            ResourceType: "Document",
            ResourceId: document.FileName,
            ResourceName: document.FileName,
            Metadata: new { source = "database-schema", provider = request.Provider },
            IpAddress: httpContext.Connection.RemoteIpAddress?.ToString()));

        return TypedResults.Ok(new IngestResultResponse(document.FileName, document.ResourceUri, document.Existed));
    }
}
