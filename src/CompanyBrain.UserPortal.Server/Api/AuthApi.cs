using System.Security.Claims;
using CompanyBrain.UserPortal.Server.Services;

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
        IUserService userService,
        IJwtService jwtService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.BadRequest(new { Error = "Email, password, and full name are required" });
        }

        if (request.Password.Length < 8)
        {
            return Results.BadRequest(new { Error = "Password must be at least 8 characters" });
        }

        var result = await userService.RegisterAsync(request.Email, request.Password, request.FullName);

        if (result.IsFailed)
        {
            return Results.BadRequest(new { Error = result.Errors.First().Message });
        }

        var user = result.Value;
        var token = jwtService.GenerateToken(user);

        return Results.Ok(new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Token = token
        });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IUserService userService,
        IJwtService jwtService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { Error = "Email and password are required" });
        }

        var result = await userService.LoginAsync(request.Email, request.Password);

        if (result.IsFailed)
        {
            return Results.BadRequest(new { Error = result.Errors.First().Message });
        }

        var user = result.Value;
        var token = jwtService.GenerateToken(user);

        return Results.Ok(new LoginResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Token = token
        });
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
            return Results.NotFound();
        }

        return Results.Ok(new UserInfo
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt
        });
    }
}

public sealed record RegisterRequest(string Email, string Password, string FullName);
public sealed record RegisterResponse
{
    public Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string Token { get; init; }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse
{
    public Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public required string Token { get; init; }
}

public sealed record UserInfo
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public DateTime CreatedAt { get; init; }
}
