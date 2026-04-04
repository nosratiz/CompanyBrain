namespace CompanyBrain.UserPortal.Server.Api.Contracts.User;

public sealed record ApiKeyResponse
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