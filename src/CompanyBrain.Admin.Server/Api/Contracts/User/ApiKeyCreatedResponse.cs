namespace CompanyBrain.Admin.Server.Api.Contracts.User;

public sealed record ApiKeyCreatedResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Key { get; init; }
    public required string Scope { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}