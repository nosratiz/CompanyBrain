using System.Security.Claims;
using CompanyBrain.Admin.Server.Api.Contracts.Shared;
using CompanyBrain.Admin.Server.Api.Contracts.User;
using CompanyBrain.Admin.Server.Api.Mapping;
using CompanyBrain.Admin.Server.Api.Validation;
using CompanyBrain.Admin.Server.Services.Interfaces;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api;

public static class UserApi
{
    public static IEndpointRouteBuilder MapUserApi(this IEndpointRouteBuilder endpoints)
    {
        var licensesGroup = endpoints.MapGroup("/api/user/licenses")
            .WithTags("User Licenses")
            .RequireAuthorization();

        licensesGroup.MapGet("/", GetUserLicensesAsync);
        licensesGroup.MapGet("/active", GetActiveLicenseAsync);
        licensesGroup.MapPost("/purchase", PurchaseLicenseAsync);

        var apiKeysGroup = endpoints.MapGroup("/api/user/api-keys")
            .WithTags("API Keys")
            .RequireAuthorization();

        apiKeysGroup.MapGet("/", GetUserApiKeysAsync);
        apiKeysGroup.MapPost("/", CreateApiKeyAsync);
        apiKeysGroup.MapDelete("/{keyId}", RevokeApiKeyAsync);

        return endpoints;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    #region Licenses

    private static async Task<IResult> GetUserLicensesAsync(
        ClaimsPrincipal principal,
        IUserLicenseService licenseService)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        var licenses = await licenseService.GetUserLicensesAsync(userId.Value);

        return TypedResults.Ok(licenses.Select(AdminApiMapper.ToLicenseResponse));
    }

    private static async Task<IResult> GetActiveLicenseAsync(
        ClaimsPrincipal principal,
        IUserLicenseService licenseService)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        var license = await licenseService.GetActiveLicenseAsync(userId.Value);

        if (license is null)
        {
            return TypedResults.NotFound(new ErrorResponse("No active license found"));
        }

        return TypedResults.Ok(AdminApiMapper.ToLicenseResponse(license));
    }

    private static async Task<IResult> PurchaseLicenseAsync(
        PurchaseLicenseRequest request,
        ClaimsPrincipal principal,
        IUserLicenseService licenseService,
        IValidator<PurchaseLicenseRequest> validator,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        if (!AdminApiMapper.TryMapLicenseTier(request.Tier, out var tier))
        {
            return TypedResults.BadRequest(new ErrorResponse("Invalid license tier"));
        }

        var result = await licenseService.PurchaseLicenseAsync(userId.Value, tier);

        if (result.IsFailed)
        {
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));
        }

        return TypedResults.Ok(AdminApiMapper.ToLicenseResponse(result.Value));
    }

    #endregion

    #region API Keys

    private static async Task<IResult> GetUserApiKeysAsync(
        ClaimsPrincipal principal,
        IUserApiKeyService apiKeyService,
        bool includeRevoked = false)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        var keys = await apiKeyService.GetUserApiKeysAsync(userId.Value, includeRevoked);

        return TypedResults.Ok(keys.Select(AdminApiMapper.ToApiKeyResponse));
    }

    private static async Task<IResult> CreateApiKeyAsync(
        CreateApiKeyRequest request,
        ClaimsPrincipal principal,
        IUserApiKeyService apiKeyService,
        IValidator<CreateApiKeyRequest> validator,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var scope = AdminApiMapper.MapApiKeyScope(request.Scope);

        var result = await apiKeyService.CreateApiKeyAsync(userId.Value, request.Name, scope, request.ExpiresAt);

        if (result.IsFailed)
        {
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));
        }

        var (plainKey, apiKey) = result.Value;
        return TypedResults.Ok(AdminApiMapper.ToApiKeyCreatedResponse(plainKey, apiKey));
    }

    private static async Task<IResult> RevokeApiKeyAsync(
        Guid keyId,
        ClaimsPrincipal principal,
        IUserApiKeyService apiKeyService)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        var result = await apiKeyService.RevokeApiKeyAsync(userId.Value, keyId);

        if (result.IsFailed)
        {
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));
        }

        return TypedResults.Ok(new MessageResponse("API key revoked successfully"));
    }

    #endregion
}
