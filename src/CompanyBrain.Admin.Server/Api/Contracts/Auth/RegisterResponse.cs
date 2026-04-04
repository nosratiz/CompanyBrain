namespace CompanyBrain.Admin.Server.Api.Contracts.Auth;

public sealed record RegisterResponse
{
    public Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string Token { get; init; }
}