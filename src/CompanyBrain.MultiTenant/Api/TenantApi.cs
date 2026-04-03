using CompanyBrain.MultiTenant.Api.Contracts;
using CompanyBrain.MultiTenant.Domain;
using CompanyBrain.MultiTenant.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CompanyBrain.MultiTenant.Api;

public static class TenantApi
{
    public static IEndpointRouteBuilder MapTenantApi(this IEndpointRouteBuilder endpoints, string mcpServerUrl)
    {
        var group = endpoints.MapGroup("/api/tenants")
            .WithTags("Tenants");

        // Tenant CRUD
        group.MapPost("/", CreateTenantAsync)
            .WithName("CreateTenant")
            .Produces<TenantResponse>(StatusCodes.Status201Created);

        group.MapGet("/", ListTenantsAsync)
            .WithName("ListTenants")
            .Produces<TenantListResponse>();

        group.MapGet("/{tenantId:guid}", GetTenantAsync)
            .WithName("GetTenant")
            .Produces<TenantResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{tenantId:guid}/plan", UpdateTenantPlanAsync)
            .WithName("UpdateTenantPlan")
            .Produces<TenantResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{tenantId:guid}/suspend", SuspendTenantAsync)
            .WithName("SuspendTenant")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // API Keys
        var keysGroup = group.MapGroup("/{tenantId:guid}/api-keys")
            .WithTags("API Keys");

        keysGroup.MapPost("/", CreateApiKeyAsync)
            .WithName("CreateApiKey")
            .Produces<ApiKeyCreatedResponse>(StatusCodes.Status201Created);

        keysGroup.MapGet("/", ListApiKeysAsync)
            .WithName("ListApiKeys")
            .Produces<ApiKeyListResponse>();

        keysGroup.MapDelete("/{keyId:guid}", RevokeApiKeyAsync)
            .WithName("RevokeApiKey")
            .Produces(StatusCodes.Status204NoContent);

        keysGroup.MapPost("/{keyId:guid}/regenerate", RegenerateApiKeyAsync)
            .WithName("RegenerateApiKey")
            .Produces<ApiKeyCreatedResponse>();

        // Storage stats
        group.MapGet("/{tenantId:guid}/storage", GetStorageStatsAsync)
            .WithName("GetTenantStorageStats")
            .Produces<TenantStorageStatsResponse>();

        // MCP Connection info
        group.MapGet("/{tenantId:guid}/mcp-connection", (Guid tenantId) => GetMcpConnectionInfo(mcpServerUrl))
            .WithName("GetMcpConnectionInfo")
            .Produces<McpConnectionInfoResponse>();

        return endpoints;
    }

    private static async Task<IResult> CreateTenantAsync(
        CreateTenantRequest request,
        [FromServices] TenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.CreateTenantAsync(
            request.Name,
            request.Description,
            request.Plan,
            cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var tenant = result.Value;
        return Results.Created($"/api/tenants/{tenant.Id}", MapToResponse(tenant));
    }

    private static async Task<IResult> ListTenantsAsync(
        [FromServices] TenantService tenantService,
        CancellationToken cancellationToken)
    {
        var tenants = await tenantService.ListTenantsAsync(cancellationToken);
        var response = new TenantListResponse(
            tenants.Select(t => new TenantSummaryResponse(
                t.Id, t.Name, t.Slug, t.Status, t.Plan, t.CreatedAt)).ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> GetTenantAsync(
        Guid tenantId,
        [FromServices] TenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.GetTenantAsync(tenantId, cancellationToken);

        if (result.IsFailed)
        {
            return Results.NotFound(new { error = result.Errors.First().Message });
        }

        return Results.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> UpdateTenantPlanAsync(
        Guid tenantId,
        UpdateTenantPlanRequest request,
        [FromServices] TenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.UpdatePlanAsync(tenantId, request.Plan, cancellationToken);

        if (result.IsFailed)
        {
            return Results.NotFound(new { error = result.Errors.First().Message });
        }

        return Results.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> SuspendTenantAsync(
        Guid tenantId,
        [FromServices] TenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.SuspendTenantAsync(tenantId, cancellationToken);

        if (result.IsFailed)
        {
            return Results.NotFound(new { error = result.Errors.First().Message });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> CreateApiKeyAsync(
        Guid tenantId,
        CreateApiKeyRequest request,
        [FromServices] ApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var result = await apiKeyService.CreateApiKeyAsync(
            tenantId,
            request.Name,
            request.Scope,
            request.ExpiresAt,
            cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var (plainKey, entity) = result.Value;
        var response = new ApiKeyCreatedResponse(
            entity.Id,
            entity.Name,
            entity.KeyPrefix,
            plainKey,
            entity.Scope,
            entity.CreatedAt,
            entity.ExpiresAt);

        return Results.Created($"/api/tenants/{tenantId}/api-keys/{entity.Id}", response);
    }

    private static async Task<IResult> ListApiKeysAsync(
        Guid tenantId,
        [FromQuery] bool includeRevoked,
        [FromServices] ApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var keys = await apiKeyService.ListApiKeysAsync(tenantId, includeRevoked, cancellationToken);
        var response = new ApiKeyListResponse(
            keys.Select(k => new ApiKeyResponse(
                k.Id, k.Name, k.KeyPrefix, k.Scope, k.CreatedAt,
                k.ExpiresAt, k.LastUsedAt, k.IsRevoked,
                k.RequestsPerMinute, k.RequestsPerDay)).ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> RevokeApiKeyAsync(
        Guid tenantId,
        Guid keyId,
        [FromBody] RevokeApiKeyRequest? request,
        [FromServices] ApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var result = await apiKeyService.RevokeApiKeyAsync(tenantId, keyId, request?.Reason, cancellationToken);

        if (result.IsFailed)
        {
            return Results.NotFound(new { error = result.Errors.First().Message });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RegenerateApiKeyAsync(
        Guid tenantId,
        Guid keyId,
        [FromServices] ApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var result = await apiKeyService.RegenerateApiKeyAsync(tenantId, keyId, cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var (plainKey, entity) = result.Value;
        return Results.Ok(new ApiKeyCreatedResponse(
            entity.Id,
            entity.Name,
            entity.KeyPrefix,
            plainKey,
            entity.Scope,
            entity.CreatedAt,
            entity.ExpiresAt));
    }

    private static async Task<IResult> GetStorageStatsAsync(
        Guid tenantId,
        [FromServices] TenantService tenantService,
        [FromServices] TenantKnowledgeStoreFactory storeFactory,
        CancellationToken cancellationToken)
    {
        var tenantResult = await tenantService.GetTenantAsync(tenantId, cancellationToken);

        if (tenantResult.IsFailed)
        {
            return Results.NotFound(new { error = tenantResult.Errors.First().Message });
        }

        var tenant = tenantResult.Value;
        var stats = storeFactory.GetStorageStats(tenantId, tenant.Slug);

        return Results.Ok(new TenantStorageStatsResponse(
            stats.DocumentCount,
            stats.TotalBytes,
            stats.FormattedSize,
            tenant.MaxStorageBytes,
            tenant.MaxStorageBytes > 0 ? (double)stats.TotalBytes / tenant.MaxStorageBytes * 100 : 0));
    }

    private static McpConnectionInfoResponse GetMcpConnectionInfo(string serverUrl) =>
        new(
            serverUrl,
            "HTTP with SSE",
            ["Claude Desktop", "GitHub Copilot (VS Code)", "Continue.dev", "Cursor"]);

    private static TenantResponse MapToResponse(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Description,
            tenant.Status,
            tenant.Plan,
            tenant.MaxDocuments,
            tenant.MaxApiKeys,
            tenant.MaxStorageBytes,
            tenant.CreatedAt,
            tenant.ApiKeys.Count(k => !k.IsRevoked),
            tenant.Users.Count(u => u.IsActive));
}
