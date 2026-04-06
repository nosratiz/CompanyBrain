using FluentResults;

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

    public static TenantUser Register(Guid tenantId, string email, string displayName, string password) => new()
    {
        TenantId = tenantId,
        Email = NormalizeEmail(email),
        DisplayName = displayName.Trim(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = TenantRole.User
    };

    public Result SignIn(string password)
    {
        if (!IsActive || string.IsNullOrEmpty(PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, PasswordHash))
        {
            return Result.Fail("Invalid email or password.");
        }

        LastLoginAt = DateTime.UtcNow;
        return Result.Ok();
    }

    public void UpdateDisplayName(string displayName) => DisplayName = displayName.Trim();

    public Result ChangePassword(string currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(PasswordHash) || !BCrypt.Net.BCrypt.Verify(currentPassword, PasswordHash))
        {
            return Result.Fail("Current password is incorrect.");
        }

        PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        return Result.Ok();
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

public enum TenantRole
{
    User,
    Admin,
    Owner
}
