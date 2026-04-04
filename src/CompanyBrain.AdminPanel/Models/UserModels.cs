namespace CompanyBrain.AdminPanel.Models;

public sealed record UserInfo(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    UserInfo User);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName);

public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string Message);

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
    string PlainKey,
    string KeyPrefix,
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
    int CurrentApiKeyCount);
