using CompanyBrain.UserPortal.Server.Api.Contracts.Auth;
using CompanyBrain.UserPortal.Server.Api.Contracts.User;
using CompanyBrain.UserPortal.Server.Domain;
using CompanyBrain.UserPortal.Server.Domain.Enums;

namespace CompanyBrain.UserPortal.Server.Api.Mapping;

internal static class UserPortalApiMapper
{
    public static RegisterResponse ToRegisterResponse(User user, string token) => new()
    {
        UserId = user.Id,
        Email = user.Email,
        Token = token
    };

    public static LoginResponse ToLoginResponse(User user, string token) => new()
    {
        UserId = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Token = token
    };

    public static UserInfoResponse ToUserInfoResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        CreatedAt = user.CreatedAt
    };

    public static LicenseResponse ToLicenseResponse(License license) => new()
    {
        Id = license.Id,
        PlanName = license.PlanName,
        Tier = license.Tier.ToString(),
        PurchasedAt = license.PurchasedAt,
        ExpiresAt = license.ExpiresAt,
        MaxApiKeys = license.MaxApiKeys,
        MaxDocuments = license.MaxDocuments,
        MaxStorageBytes = license.MaxStorageBytes,
        IsActive = license.IsActive
    };

    public static ApiKeyResponse ToApiKeyResponse(UserApiKey apiKey) => new()
    {
        Id = apiKey.Id,
        Name = apiKey.Name,
        KeyPrefix = $"{apiKey.KeyPrefix}***",
        Scope = apiKey.Scope.ToString(),
        CreatedAt = apiKey.CreatedAt,
        ExpiresAt = apiKey.ExpiresAt,
        LastUsedAt = apiKey.LastUsedAt,
        IsRevoked = apiKey.IsRevoked
    };

    public static ApiKeyCreatedResponse ToApiKeyCreatedResponse(string plainKey, UserApiKey apiKey) => new()
    {
        Id = apiKey.Id,
        Name = apiKey.Name,
        Key = plainKey,
        Scope = apiKey.Scope.ToString(),
        CreatedAt = apiKey.CreatedAt,
        ExpiresAt = apiKey.ExpiresAt
    };

    public static bool TryMapLicenseTier(string value, out LicenseTier tier) =>
        Enum.TryParse(value, ignoreCase: true, out tier);

    public static ApiKeyScope MapApiKeyScope(string? value) =>
        Enum.TryParse<ApiKeyScope>(value, ignoreCase: true, out var scope)
            ? scope
            : ApiKeyScope.ReadOnly;
}