using System.Security.Claims;
using CompanyBrain.MultiTenant.Api.Validation;
using CompanyBrain.MultiTenant.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CompanyBrain.MultiTenant.Api;

public static class AuthApi
{
    public static IEndpointRouteBuilder MapAuthApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .RequireAuthorization()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        [FromServices] IUserService userService,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<RegisterRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.RegisterAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            request.TenantId,
            cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: string.Join(", ", result.Errors.Select(e => e.Message)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = result.Value;
        var token = await jwtService.GenerateTokenAsync(user, cancellationToken);

        return Results.Created($"/api/auth/me", new AuthResponse(
            Token: token,
            User: new UserResponse(
                Id: user.Id,
                Email: user.Email,
                DisplayName: user.DisplayName,
                TenantId: user.TenantId,
                Role: user.Role.ToString(),
                CreatedAt: user.CreatedAt,
                LastLoginAt: user.LastLoginAt)));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        [FromServices] IUserService userService,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<LoginRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.LoginAsync(request.Email, request.Password, cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = result.Value;
        var token = await jwtService.GenerateTokenAsync(user, cancellationToken);

        return Results.Ok(new AuthResponse(
            Token: token,
            User: new UserResponse(
                Id: user.Id,
                Email: user.Email,
                DisplayName: user.DisplayName,
                TenantId: user.TenantId,
                Role: user.Role.ToString(),
                CreatedAt: user.CreatedAt,
                LastLoginAt: user.LastLoginAt)));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        [FromServices] IUserService userService,
        CancellationToken cancellationToken)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Problem(
                detail: "Invalid token.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await userService.GetUserByIdAsync(userId, cancellationToken);

        if (result.IsFailed)
        {
            return Results.Problem(
                detail: "User not found.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = result.Value;
        return Results.Ok(new UserResponse(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            TenantId: user.TenantId,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt));
    }
}

#region Contracts

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    Guid TenantId);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record AuthResponse(
    string Token,
    UserResponse User);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    Guid TenantId,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

#endregion
