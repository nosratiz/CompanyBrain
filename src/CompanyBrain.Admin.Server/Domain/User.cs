using CompanyBrain.Admin.Server.Domain.Enums;
using FluentResults;

namespace CompanyBrain.Admin.Server.Domain;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<License> Licenses { get; set; } = [];
    public ICollection<UserApiKey> ApiKeys { get; set; } = [];

    public static User Register(string email, string password, string fullName) => new()
    {
        Email = NormalizeEmail(email),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        FullName = fullName.Trim()
    };

    public Result SignIn(string password)
    {
        if (!IsActive)
        {
            return Result.Fail("Account is deactivated");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, PasswordHash))
        {
            return Result.Fail("Invalid email or password");
        }

        LastLoginAt = DateTime.UtcNow;
        return Result.Ok();
    }

    public void UpdateProfile(string? fullName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = NormalizeEmail(email);
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            FullName = fullName.Trim();
        }
    }

    public void SetActiveStatus(bool isActive) => IsActive = isActive;

    public License CreateLicense(LicenseTier tier) => License.Create(Id, tier);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}