using CompanyBrain.Admin.Server.Domain.Enums;

namespace CompanyBrain.Admin.Server.Domain;

public sealed class UserApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string KeyHash { get; set; }
    public required string KeyPrefix { get; set; }
    public ApiKeyScope Scope { get; set; } = ApiKeyScope.ReadOnly;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public string? RevokedReason { get; set; }

    public User? User { get; set; }
}