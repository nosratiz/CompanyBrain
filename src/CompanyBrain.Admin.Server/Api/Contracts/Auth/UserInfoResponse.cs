namespace CompanyBrain.Admin.Server.Api.Contracts.Auth;

public sealed record UserInfoResponse
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public DateTime CreatedAt { get; init; }
}