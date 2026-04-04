using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.UserPortal.Server.Data;
using CompanyBrain.UserPortal.Server.Domain;
using CompanyBrain.UserPortal.Server.Domain.Enums;
using CompanyBrain.UserPortal.Server.Services.Interfaces;

namespace CompanyBrain.UserPortal.Server.Services;

public sealed class UserApiKeyService : IUserApiKeyService
{
    private const string KeyPrefix = "cb_";
    private readonly UserDbContext _dbContext;
    private readonly IUserLicenseService _licenseService;

    public UserApiKeyService(UserDbContext dbContext, IUserLicenseService licenseService)
    {
        _dbContext = dbContext;
        _licenseService = licenseService;
    }

    public async Task<IReadOnlyList<UserApiKey>> GetUserApiKeysAsync(Guid userId, bool includeRevoked = false)
    {
        var query = _dbContext.ApiKeys.Where(k => k.UserId == userId);

        if (!includeRevoked)
        {
            query = query.Where(k => !k.IsRevoked);
        }

        return await query.OrderByDescending(k => k.CreatedAt).ToListAsync();
    }

    public async Task<Result<(string PlainKey, UserApiKey ApiKey)>> CreateApiKeyAsync(
        Guid userId, string name, ApiKeyScope scope, DateTime? expiresAt)
    {
        // Check license limits
        var license = await _licenseService.GetActiveLicenseAsync(userId);
        if (license is null)
        {
            return Result.Fail<(string, UserApiKey)>("No active license found");
        }

        var currentKeyCount = await _dbContext.ApiKeys
            .CountAsync(k => k.UserId == userId && !k.IsRevoked);

        if (currentKeyCount >= license.MaxApiKeys)
        {
            return Result.Fail<(string, UserApiKey)>($"API key limit reached ({license.MaxApiKeys}). Upgrade your plan to create more.");
        }

        // Generate key
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var keyPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var plainKey = $"{KeyPrefix}{keyPart}";
        var keyHash = HashKey(plainKey);
        var keyPrefixStored = plainKey[..11];

        var apiKey = new UserApiKey
        {
            UserId = userId,
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefixStored,
            Scope = scope,
            ExpiresAt = expiresAt
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync();

        return Result.Ok((plainKey, apiKey));
    }

    public async Task<Result> RevokeApiKeyAsync(Guid userId, Guid keyId)
    {
        var apiKey = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId);

        if (apiKey is null)
        {
            return Result.Fail("API key not found");
        }

        if (apiKey.IsRevoked)
        {
            return Result.Fail("API key is already revoked");
        }

        apiKey.IsRevoked = true;
        apiKey.RevokedReason = "User revoked";
        await _dbContext.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result<(Guid UserId, ApiKeyScope Scope)>> ValidateApiKeyAsync(string plainKey)
    {
        if (string.IsNullOrWhiteSpace(plainKey) || !plainKey.StartsWith(KeyPrefix))
        {
            return Result.Fail<(Guid, ApiKeyScope)>("Invalid API key format");
        }

        var keyHash = HashKey(plainKey);
        var keyPrefix = plainKey[..11];

        var apiKey = await _dbContext.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix && k.KeyHash == keyHash);

        if (apiKey is null)
        {
            return Result.Fail<(Guid, ApiKeyScope)>("Invalid API key");
        }

        if (apiKey.IsRevoked)
        {
            return Result.Fail<(Guid, ApiKeyScope)>("API key has been revoked");
        }

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt < DateTime.UtcNow)
        {
            return Result.Fail<(Guid, ApiKeyScope)>("API key has expired");
        }

        if (apiKey.User is null || !apiKey.User.IsActive)
        {
            return Result.Fail<(Guid, ApiKeyScope)>("User account is not active");
        }

        // Update last used
        apiKey.LastUsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Result.Ok((apiKey.UserId, apiKey.Scope));
    }

    private static string HashKey(string plainKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToBase64String(bytes);
    }
}
