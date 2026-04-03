using System.Security.Claims;
using CompanyBrain.UserPortal.Server.Domain;
using CompanyBrain.UserPortal.Server.Services;

namespace CompanyBrain.UserPortal.Server.Api;

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

        return Results.Ok(licenses.Select(l => new LicenseDto
        {
            Id = l.Id,
            PlanName = l.PlanName,
            Tier = l.Tier.ToString(),
            PurchasedAt = l.PurchasedAt,
            ExpiresAt = l.ExpiresAt,
            MaxApiKeys = l.MaxApiKeys,
            MaxDocuments = l.MaxDocuments,
            MaxStorageBytes = l.MaxStorageBytes,
            IsActive = l.IsActive
        }));
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
            return Results.NotFound(new { Error = "No active license found" });
        }

        return Results.Ok(new LicenseDto
        {
            Id = license.Id,
            PlanName = license.PlanName,
            Tier = license.Tier.ToString(),
            PurchasedAt = license.PurchasedAt,
            ExpiresAt = license.ExpiresAt,
            MaxApiKeys = license.MaxApiKeys,
            MaxDocuments = license.MaxDocuments,
            MaxStorageBytes = license.MaxStorageBytes,
            IsActive = license.IsActive
        });
    }

    private static async Task<IResult> PurchaseLicenseAsync(
        PurchaseLicenseRequest request,
        ClaimsPrincipal principal,
        IUserLicenseService licenseService)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        if (!Enum.TryParse<LicenseTier>(request.Tier, ignoreCase: true, out var tier))
        {
            return Results.BadRequest(new { Error = "Invalid license tier" });
        }

        var result = await licenseService.PurchaseLicenseAsync(userId.Value, tier);

        if (result.IsFailed)
        {
            return Results.BadRequest(new { Error = result.Errors.First().Message });
        }

        var license = result.Value;
        return Results.Ok(new LicenseDto
        {
            Id = license.Id,
            PlanName = license.PlanName,
            Tier = license.Tier.ToString(),
            PurchasedAt = license.PurchasedAt,
            ExpiresAt = license.ExpiresAt,
            MaxApiKeys = license.MaxApiKeys,
            MaxDocuments = license.MaxDocuments,
            MaxStorageBytes = license.MaxStorageBytes,
            IsActive = license.IsActive
        });
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

        return Results.Ok(keys.Select(k => new ApiKeyDto
        {
            Id = k.Id,
            Name = k.Name,
            KeyPrefix = k.KeyPrefix + "***",
            Scope = k.Scope.ToString(),
            CreatedAt = k.CreatedAt,
            ExpiresAt = k.ExpiresAt,
            LastUsedAt = k.LastUsedAt,
            IsRevoked = k.IsRevoked
        }));
    }

    private static async Task<IResult> CreateApiKeyAsync(
        CreateApiKeyRequest request,
        ClaimsPrincipal principal,
        IUserApiKeyService apiKeyService)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { Error = "Name is required" });
        }

        if (!Enum.TryParse<ApiKeyScope>(request.Scope, ignoreCase: true, out var scope))
        {
            scope = ApiKeyScope.ReadOnly;
        }

        var result = await apiKeyService.CreateApiKeyAsync(userId.Value, request.Name, scope, request.ExpiresAt);

        if (result.IsFailed)
        {
            return Results.BadRequest(new { Error = result.Errors.First().Message });
        }

        var (plainKey, apiKey) = result.Value;

        return Results.Ok(new ApiKeyCreatedResponse
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            Key = plainKey,
            Scope = apiKey.Scope.ToString(),
            CreatedAt = apiKey.CreatedAt,
            ExpiresAt = apiKey.ExpiresAt
        });
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
            return Results.BadRequest(new { Error = result.Errors.First().Message });
        }

        return Results.Ok(new { Message = "API key revoked successfully" });
    }

    #endregion
}

#region DTOs

public sealed record LicenseDto
{
    public Guid Id { get; init; }
    public required string PlanName { get; init; }
    public required string Tier { get; init; }
    public DateTime PurchasedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int MaxApiKeys { get; init; }
    public int MaxDocuments { get; init; }
    public long MaxStorageBytes { get; init; }
    public bool IsActive { get; init; }
}

public sealed record PurchaseLicenseRequest(string Tier);

public sealed record ApiKeyDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string KeyPrefix { get; init; }
    public required string Scope { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public bool IsRevoked { get; init; }
}

public sealed record CreateApiKeyRequest(string Name, string? Scope, DateTime? ExpiresAt);

public sealed record ApiKeyCreatedResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Key { get; init; }
    public required string Scope { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

#endregion
