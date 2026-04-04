using System.Security.Claims;
using CompanyBrain.UserPortal.Server.Api.Contracts.Auth;
using CompanyBrain.UserPortal.Server.Api.Contracts.Shared;
using CompanyBrain.UserPortal.Server.Api.Mapping;
using CompanyBrain.UserPortal.Server.Api.Validation;
using CompanyBrain.UserPortal.Server.Services.Interfaces;
using FluentValidation;

namespace CompanyBrain.UserPortal.Server.Api;

public static class AuthApi
{
    public static IEndpointRouteBuilder MapAuthApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IValidator<RegisterRequest> validator,
        IUserService userService,
        IJwtService jwtService,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.RegisterAsync(request.Email, request.Password, request.FullName);

        if (result.IsFailed)
        {
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));
        }

        var user = result.Value;
        var token = jwtService.GenerateToken(user);

        return TypedResults.Ok(UserPortalApiMapper.ToRegisterResponse(user, token));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IUserService userService,
        IJwtService jwtService,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationProblem();
        }

        var result = await userService.LoginAsync(request.Email, request.Password);

        if (result.IsFailed)
        {
            return TypedResults.BadRequest(new ErrorResponse(result.Errors.First().Message));
        }

        var user = result.Value;
        var token = jwtService.GenerateToken(user);

        return TypedResults.Ok(UserPortalApiMapper.ToLoginResponse(user, token));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IUserService userService)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await userService.GetByIdAsync(userId);
        if (user is null)
        {
            return TypedResults.NotFound(new ErrorResponse("User not found"));
        }

        return TypedResults.Ok(UserPortalApiMapper.ToUserInfoResponse(user));
    }
}
