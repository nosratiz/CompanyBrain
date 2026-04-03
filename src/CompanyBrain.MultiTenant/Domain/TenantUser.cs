namespace CompanyBrain.MultiTenant.Domain;

/// <summary>
/// A user belonging to a tenant with specific roles.
/// </summary>
public sealed class TenantUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public string? PasswordHash { get; set; }
    public TenantRole Role { get; set; } = TenantRole.User;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Tenant? Tenant { get; init; }
}

public enum TenantRole
{
    User,
    Admin,
    Owner
}
