using CompanyBrain.Dashboard.Api.Validation;
using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Features.DocumentTenant.Requests;
using CompanyBrain.Dashboard.Features.DocumentTenant.Responses;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Dashboard.Services.Dtos;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.DocumentTenant;

/// <summary>
/// API endpoints for managing document-tenant assignments.
/// </summary>
internal static class DocumentTenantApi
{
    public static IEndpointRouteBuilder MapDocumentTenantApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/document-tenants")
            .WithTags("Document-Tenant Assignments");

        group.MapGet("/", GetAllAssignmentsAsync)
            .WithName("GetAllDocumentTenantAssignments")
            .Produces<DocumentTenantAssignmentListResponse>();

        group.MapGet("/by-document/{**fileName}", GetAssignmentsByDocumentAsync)
            .WithName("GetAssignmentsByDocument")
            .Produces<DocumentAssignmentsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/by-tenant/{tenantId:guid}", GetAssignmentsByTenantAsync)
            .WithName("GetAssignmentsByTenant")
            .Produces<DocumentTenantAssignmentListResponse>();

        group.MapPost("/", AssignDocumentToTenantAsync)
            .WithName("AssignDocumentToTenant")
            .Produces<DocumentTenantAssignmentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .DisableAntiforgery();

        group.MapPut("/by-document/{**fileName}", UpdateDocumentTenantsAsync)
            .WithName("UpdateDocumentTenants")
            .Produces<DocumentAssignmentsResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .DisableAntiforgery();

        group.MapDelete("/{id:int}", RemoveAssignmentAsync)
            .WithName("RemoveDocumentTenantAssignment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/by-document/tenant/{tenantId:guid}/{**fileName}", RemoveAssignmentByDocumentAndTenantAsync)
            .WithName("RemoveAssignmentByDocumentAndTenant")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/available-tenants", GetAvailableTenantsAsync)
            .WithName("GetAvailableTenants")
            .Produces<IReadOnlyList<TenantSummaryDto>>();

        return endpoints;
    }

    private static async Task<IResult> GetAllAssignmentsAsync(
        [FromServices] DocumentAssignmentDbContext db,
        CancellationToken ct)
    {
        var assignments = await db.DocumentTenantAssignments
            .AsNoTracking()
            .OrderBy(a => a.FileName)
            .ThenBy(a => a.TenantName)
            .ToListAsync(ct);

        return Results.Ok(new DocumentTenantAssignmentListResponse(
            assignments.Select(MapToResponse).ToList()));
    }

    private static async Task<IResult> GetAssignmentsByDocumentAsync(
        string fileName,
        [FromServices] DocumentAssignmentDbContext db,
        CancellationToken ct)
    {
        var decodedFileName = Uri.UnescapeDataString(fileName);
        
        var assignments = await db.DocumentTenantAssignments
            .AsNoTracking()
            .Where(a => a.FileName == decodedFileName)
            .OrderBy(a => a.TenantName)
            .ToListAsync(ct);

        return Results.Ok(new DocumentAssignmentsResponse(
            decodedFileName,
            assignments.Select(a => new TenantAssignmentInfo(a.TenantId, a.TenantName, a.CreatedAtUtc)).ToList()));
    }

    private static async Task<IResult> GetAssignmentsByTenantAsync(
        Guid tenantId,
        [FromServices] DocumentAssignmentDbContext db,
        CancellationToken ct)
    {
        var assignments = await db.DocumentTenantAssignments
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.FileName)
            .ToListAsync(ct);

        return Results.Ok(new DocumentTenantAssignmentListResponse(
            assignments.Select(MapToResponse).ToList()));
    }

    private static async Task<IResult> AssignDocumentToTenantAsync(
        AssignDocumentToTenantRequest request,
        [FromServices] IValidator<AssignDocumentToTenantRequest> validator,
        [FromServices] DocumentAssignmentDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var existing = await db.DocumentTenantAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FileName == request.FileName && a.TenantId == request.TenantId, ct);

        if (existing is not null)
        {
            return Results.Problem(
                title: "Conflict",
                detail: $"Document '{request.FileName}' is already assigned to tenant '{request.TenantName}'.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var assignment = new DocumentTenantAssignment
        {
            CollectionId = ExtractCollectionId(request.FileName),
            FileName = request.FileName,
            TenantId = request.TenantId,
            TenantName = request.TenantName,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.DocumentTenantAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/document-tenants/{assignment.Id}", MapToResponse(assignment));
    }

    private static async Task<IResult> UpdateDocumentTenantsAsync(
        string fileName,
        UpdateDocumentTenantsRequest request,
        [FromServices] IValidator<UpdateDocumentTenantsRequest> validator,
        [FromServices] DocumentAssignmentDbContext db,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var decodedFileName = Uri.UnescapeDataString(fileName);

        await db.DocumentTenantAssignments
            .Where(a => a.FileName == decodedFileName)
            .ExecuteDeleteAsync(ct);

        var newAssignments = request.Tenants.Select(t => new DocumentTenantAssignment
        {
            CollectionId = ExtractCollectionId(decodedFileName),
            FileName = decodedFileName,
            TenantId = t.TenantId,
            TenantName = t.TenantName,
            CreatedAtUtc = DateTime.UtcNow
        }).ToList();

        db.DocumentTenantAssignments.AddRange(newAssignments);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new DocumentAssignmentsResponse(
            decodedFileName,
            newAssignments.Select(a => new TenantAssignmentInfo(a.TenantId, a.TenantName, a.CreatedAtUtc)).ToList()));
    }

    private static async Task<IResult> RemoveAssignmentAsync(
        int id,
        [FromServices] DocumentAssignmentDbContext db,
        CancellationToken ct)
    {
        var deleted = await db.DocumentTenantAssignments
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0)
        {
            return Results.Problem(
                title: "Not Found",
                detail: $"Assignment with ID {id} not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RemoveAssignmentByDocumentAndTenantAsync(
        string fileName,
        Guid tenantId,
        [FromServices] DocumentAssignmentDbContext db,
        CancellationToken ct)
    {
        var decodedFileName = Uri.UnescapeDataString(fileName);
        
        var deleted = await db.DocumentTenantAssignments
            .Where(a => a.FileName == decodedFileName && a.TenantId == tenantId)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0)
        {
            return Results.Problem(
                title: "Not Found",
                detail: $"Assignment for document '{decodedFileName}' and tenant '{tenantId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetAvailableTenantsAsync(
        [FromServices] ExternalTenantApiClient tenantClient,
        CancellationToken ct)
    {
        var tenants = await tenantClient.GetTenantsAsync(ct);
        return Results.Ok(tenants);
    }

    private static DocumentTenantAssignmentResponse MapToResponse(DocumentTenantAssignment assignment) =>
        new(assignment.Id, assignment.FileName, assignment.TenantId, assignment.TenantName, assignment.CreatedAtUtc);

    private static string ExtractCollectionId(string fileName)
    {
        var normalized = fileName.Replace('\\', '/').Trim('/');
        var slashIndex = normalized.IndexOf('/');
        if (slashIndex < 0)
        {
            return "General";
        }

        return normalized[..slashIndex];
    }
}
