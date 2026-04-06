using System.Security.Claims;
using CompanyBrain.MultiTenant.Api.Validation;
using CompanyBrain.MultiTenant.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CompanyBrain.MultiTenant.Api;

public static class ProfileApi
{
    public static IEndpointRouteBuilder MapProfileApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        group.MapGet("/", GetProfileAsync)
            .WithName("GetProfile")
            .Produces<ProfileResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/", UpdateProfileAsync)
            .WithName("UpdateProfile")
            .Produces<ProfileResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Problem(detail: "Invalid token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await userService.GetUserByIdAsync(userId.Value, cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(detail: "User not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var user = result.Value;
        return Results.Ok(new ProfileResponse(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            TenantId: user.TenantId,
            TenantName: user.Tenant?.Name,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            IsActive: user.IsActive));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        [FromServices] IUserService userService,
        [FromServices] IValidator<UpdateProfileRequest> validator,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Problem(detail: "Invalid token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.UpdateProfileAsync(
            userId.Value,
            request.DisplayName,
            currentPassword: null,
            newPassword: null,
            cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = result.Value;
        return Results.Ok(new ProfileResponse(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            TenantId: user.TenantId,
            TenantName: user.Tenant?.Name,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            IsActive: user.IsActive));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        [FromServices] IUserService userService,
        [FromServices] IValidator<ChangePasswordRequest> validator,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Problem(detail: "Invalid token.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.UpdateProfileAsync(
            userId.Value,
            displayName: null,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

#region Contracts

public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    Guid TenantId,
    string? TenantName,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool IsActive);

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

#endregion
