using CompanyBrain.Admin.Server.Api.Contracts.Admin;
using CompanyBrain.Admin.Server.Api.Contracts.Shared;
using CompanyBrain.Admin.Server.Api.Mapping;
using CompanyBrain.Admin.Server.Api.Validation;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;
using CompanyBrain.Admin.Server.Services.Interfaces;
using FluentValidation;

namespace CompanyBrain.Admin.Server.Api;

public static class AdminApi
{
    public static IEndpointRouteBuilder MapAdminApi(this IEndpointRouteBuilder endpoints)
    {
        var usersGroup = endpoints.MapGroup("/api/admin/users")
            .WithTags("Admin - Users")
            .RequireAuthorization();

        usersGroup.MapGet("/", GetAllUsersAsync);
        usersGroup.MapGet("/{userId}", GetUserByIdAsync);
        usersGroup.MapPost("/", CreateUserAsync);
        usersGroup.MapPut("/{userId}", UpdateUserAsync);
        usersGroup.MapPut("/{userId}/status", SetUserStatusAsync);
        usersGroup.MapDelete("/{userId}", DeleteUserAsync);

        var licensesGroup = endpoints.MapGroup("/api/admin/licenses")
            .WithTags("Admin - Licenses")
            .RequireAuthorization();

        licensesGroup.MapGet("/", GetAllLicensesAsync);
        licensesGroup.MapPost("/assign", AssignLicenseAsync);
        licensesGroup.MapPut("/{licenseId}", UpdateLicenseAsync);
        licensesGroup.MapPut("/{licenseId}/revoke", RevokeLicenseAsync);

        var apiKeysGroup = endpoints.MapGroup("/api/admin/api-keys")
            .WithTags("Admin - API Keys")
            .RequireAuthorization();

        apiKeysGroup.MapGet("/", GetAllApiKeysAsync);
        apiKeysGroup.MapPut("/{keyId}/revoke", AdminRevokeApiKeyAsync);

        return endpoints;
    }

    #region Users

    private static async Task<IResult> GetAllUsersAsync(
        IUserService userService,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var users = await userService.GetAllUsersAsync(page, pageSize, cancellationToken);
        var totalCount = await userService.GetTotalUserCountAsync(cancellationToken);

        var items = users.Select(ToUserDetailResponse).ToList();

        return TypedResults.Ok(new PaginatedResponse<UserDetailResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private static async Task<IResult> GetUserByIdAsync(
        Guid userId,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var user = await userService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return TypedResults.NotFound(new ErrorResponse("User not found"));

        return TypedResults.Ok(ToUserDetailResponse(user));
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        IUserService userService,
        IValidator<CreateUserRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.RegisterAsync(request.Email, request.Password, request.FullName, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(ToUserDetailResponse(result.Value));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        IUserService userService,
        IValidator<UpdateUserRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.UpdateUserAsync(userId, request.FullName, request.Email, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(new MessageResponse("User updated successfully"));
    }

    private static async Task<IResult> SetUserStatusAsync(
        Guid userId,
        SetUserStatusRequest request,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.SetUserActiveStatusAsync(userId, request.IsActive, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        var status = request.IsActive ? "activated" : "deactivated";
        return TypedResults.Ok(new MessageResponse($"User {status} successfully"));
    }

    private static async Task<IResult> DeleteUserAsync(
        Guid userId,
        IUserService userService,
        CancellationToken cancellationToken)
    {
        var result = await userService.DeleteUserAsync(userId, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(new MessageResponse("User deleted successfully"));
    }

    #endregion

    #region Licenses

    private static async Task<IResult> GetAllLicensesAsync(
        IUserLicenseService licenseService,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var licenses = await licenseService.GetAllLicensesAsync(page, pageSize, cancellationToken);
        var totalCount = await licenseService.GetTotalLicenseCountAsync(cancellationToken);

        var items = licenses.Select(ToLicenseDetailResponse).ToList();

        return TypedResults.Ok(new PaginatedResponse<LicenseDetailResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private static async Task<IResult> AssignLicenseAsync(
        AssignLicenseRequest request,
        IUserLicenseService licenseService,
        IValidator<AssignLicenseRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var tier = Enum.Parse<LicenseTier>(request.Tier, ignoreCase: true);

        var result = await licenseService.AssignLicenseAsync(request.UserId, tier, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(AdminApiMapper.ToLicenseResponse(result.Value));
    }

    private static async Task<IResult> UpdateLicenseAsync(
        Guid licenseId,
        UpdateLicenseRequest request,
        IUserLicenseService licenseService,
        IValidator<UpdateLicenseRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var tier = Enum.Parse<LicenseTier>(request.Tier, ignoreCase: true);

        var result = await licenseService.UpdateLicenseTierAsync(licenseId, tier, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(AdminApiMapper.ToLicenseResponse(result.Value));
    }

    private static async Task<IResult> RevokeLicenseAsync(
        Guid licenseId,
        IUserLicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var result = await licenseService.RevokeLicenseAsync(licenseId, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(new MessageResponse("License revoked successfully"));
    }

    #endregion

    #region API Keys

    private static async Task<IResult> GetAllApiKeysAsync(
        IUserApiKeyService apiKeyService,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var apiKeys = await apiKeyService.GetAllApiKeysAsync(page, pageSize, cancellationToken);
        var totalCount = await apiKeyService.GetTotalApiKeyCountAsync(cancellationToken);

        var items = apiKeys.Select(ToApiKeyDetailResponse).ToList();

        return TypedResults.Ok(new PaginatedResponse<ApiKeyDetailResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private static async Task<IResult> AdminRevokeApiKeyAsync(
        Guid keyId,
        IUserApiKeyService apiKeyService,
        CancellationToken cancellationToken)
    {
        var result = await apiKeyService.AdminRevokeApiKeyAsync(keyId, cancellationToken);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(new MessageResponse("API key revoked successfully"));
    }

    #endregion

    #region Mapping Helpers

    private static UserDetailResponse ToUserDetailResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
        IsActive = user.IsActive,
        Licenses = user.Licenses.Select(l => new LicenseSummary
        {
            Id = l.Id,
            PlanName = l.PlanName,
            Tier = l.Tier.ToString(),
            IsActive = l.IsActive,
            ExpiresAt = l.ExpiresAt
        }).ToList()
    };

    private static LicenseDetailResponse ToLicenseDetailResponse(License license) => new()
    {
        Id = license.Id,
        UserId = license.UserId,
        UserEmail = license.User?.Email,
        UserFullName = license.User?.FullName,
        PlanName = license.PlanName,
        Tier = license.Tier.ToString(),
        PurchasedAt = license.PurchasedAt,
        ExpiresAt = license.ExpiresAt,
        MaxApiKeys = license.MaxApiKeys,
        MaxDocuments = license.MaxDocuments,
        MaxStorageBytes = license.MaxStorageBytes,
        IsActive = license.IsActive
    };

    private static ApiKeyDetailResponse ToApiKeyDetailResponse(UserApiKey apiKey) => new()
    {
        Id = apiKey.Id,
        UserId = apiKey.UserId,
        UserEmail = apiKey.User?.Email,
        UserFullName = apiKey.User?.FullName,
        Name = apiKey.Name,
        KeyPrefix = $"{apiKey.KeyPrefix}***",
        Scope = apiKey.Scope.ToString(),
        CreatedAt = apiKey.CreatedAt,
        ExpiresAt = apiKey.ExpiresAt,
        LastUsedAt = apiKey.LastUsedAt,
        IsRevoked = apiKey.IsRevoked
    };

    #endregion
}
