using CompanyBrain.Admin.Server.Api.Contracts.Admin;
using CompanyBrain.Admin.Server.Api.Contracts.Shared;
using CompanyBrain.Admin.Server.Api.Mapping;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Services.Interfaces;

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
        usersGroup.MapPut("/{userId}", UpdateUserAsync);
        usersGroup.MapPut("/{userId}/status", SetUserStatusAsync);
        usersGroup.MapDelete("/{userId}", DeleteUserAsync);

        var licensesGroup = endpoints.MapGroup("/api/admin/licenses")
            .WithTags("Admin - Licenses")
            .RequireAuthorization();

        licensesGroup.MapGet("/", GetAllLicensesAsync);
        licensesGroup.MapPost("/assign", AssignLicenseAsync);
        licensesGroup.MapPut("/{licenseId}/revoke", RevokeLicenseAsync);

        return endpoints;
    }

    #region Users

    private static async Task<IResult> GetAllUsersAsync(
        IUserService userService,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var users = await userService.GetAllUsersAsync(page, pageSize);
        var totalCount = await userService.GetTotalUserCountAsync();

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
        IUserService userService)
    {
        var user = await userService.GetByIdAsync(userId);
        if (user is null)
            return TypedResults.NotFound(new ErrorResponse("User not found"));

        return TypedResults.Ok(ToUserDetailResponse(user));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
        IUserService userService)
    {
        var result = await userService.UpdateUserAsync(userId, request.FullName, request.Email);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(new MessageResponse("User updated successfully"));
    }

    private static async Task<IResult> SetUserStatusAsync(
        Guid userId,
        SetUserStatusRequest request,
        IUserService userService)
    {
        var result = await userService.SetUserActiveStatusAsync(userId, request.IsActive);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        var status = request.IsActive ? "activated" : "deactivated";
        return TypedResults.Ok(new MessageResponse($"User {status} successfully"));
    }

    private static async Task<IResult> DeleteUserAsync(
        Guid userId,
        IUserService userService)
    {
        var result = await userService.DeleteUserAsync(userId);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(new MessageResponse("User deleted successfully"));
    }

    #endregion

    #region Licenses

    private static async Task<IResult> GetAllLicensesAsync(
        IUserLicenseService licenseService,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var licenses = await licenseService.GetAllLicensesAsync(page, pageSize);
        var totalCount = await licenseService.GetTotalLicenseCountAsync();

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
        IUserLicenseService licenseService)
    {
        if (!AdminApiMapper.TryMapLicenseTier(request.Tier, out var tier))
            return TypedResults.BadRequest(new ErrorResponse("Invalid license tier"));

        var result = await licenseService.AssignLicenseAsync(request.UserId, tier);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(AdminApiMapper.ToLicenseResponse(result.Value));
    }

    private static async Task<IResult> RevokeLicenseAsync(
        Guid licenseId,
        IUserLicenseService licenseService)
    {
        var result = await licenseService.RevokeLicenseAsync(licenseId);
        if (result.IsFailed)
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));

        return TypedResults.Ok(new MessageResponse("License revoked successfully"));
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

    #endregion
}
