using System.Text.Json.Serialization;

namespace CompanyBrain.AdminPanel.Models;

public sealed record UserInfo(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Token);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName);

public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string Token);

public sealed record UserLicense(
    Guid Id,
    string PlanName,
    LicenseTier Tier,
    DateTime PurchasedAt,
    DateTime? ExpiresAt,
    int MaxApiKeys,
    int MaxDocuments,
    long MaxStorageBytes,
    bool IsActive);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LicenseTier
{
    Free,
    Starter,
    Professional,
    Enterprise
}

public sealed record UserApiKey(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Scope,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked);

public sealed record CreateApiKeyRequest(
    string Name,
    string Scope,
    DateTime? ExpiresAt);

public sealed record ApiKeyCreatedResponse(
    Guid Id,
    string Name,
    string Key,
    string Scope,
    DateTime CreatedAt);

public sealed record PurchaseLicenseRequest(LicenseTier Tier);

public sealed record LicenseResponse(
    Guid Id,
    string PlanName,
    LicenseTier Tier,
    DateTime PurchasedAt,
    DateTime? ExpiresAt,
    int MaxApiKeys,
    int MaxDocuments,
    long MaxStorageBytes,
    bool IsActive);

// Admin DTOs

public sealed record AdminUserDetail
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<AdminLicenseSummary> Licenses { get; init; } = [];
}

public sealed record AdminLicenseSummary
{
    public Guid Id { get; init; }
    public required string PlanName { get; init; }
    public required string Tier { get; init; }
    public bool IsActive { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public sealed record AdminLicenseDetail
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? UserFullName { get; init; }
    public required string PlanName { get; init; }
    public required string Tier { get; init; }
    public DateTime PurchasedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int MaxApiKeys { get; init; }
    public int MaxDocuments { get; init; }
    public long MaxStorageBytes { get; init; }
    public bool IsActive { get; init; }
}

public sealed record PaginatedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record UpdateUserRequest(string? FullName, string? Email);

public sealed record SetUserStatusRequest(bool IsActive);

public sealed record AssignLicenseRequest(Guid UserId, string Tier);

public sealed record MessageResponse(string Message);

public sealed record CreateUserRequest(string Email, string Password, string FullName);

public sealed record UpdateProfileRequest(string? FullName, string? Email);

public sealed record UpdateLicenseRequest(string Tier);

public sealed record AdminApiKeyDetail
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? UserFullName { get; init; }
    public required string Name { get; init; }
    public required string KeyPrefix { get; init; }
    public required string Scope { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public bool IsRevoked { get; init; }
}
