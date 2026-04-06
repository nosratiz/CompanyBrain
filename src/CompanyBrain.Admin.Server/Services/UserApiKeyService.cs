using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.Admin.Server.Data;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;
using CompanyBrain.Admin.Server.Services.Interfaces;

namespace CompanyBrain.Admin.Server.Services;

public sealed class UserApiKeyService(UserDbContext dbContext, IUserLicenseService licenseService) : IUserApiKeyService
{
    private const string KeyPrefix = "cb_";

    public async Task<IReadOnlyList<UserApiKey>> GetUserApiKeysAsync(Guid userId, bool includeRevoked = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ApiKeys.Where(k => k.UserId == userId);

        if (!includeRevoked)
        {
            query = query.Where(k => !k.IsRevoked);
        }

        return await query.OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserApiKey>> GetAllApiKeysAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await dbContext.ApiKeys
            .Include(k => k.User)
            .OrderByDescending(k => k.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalApiKeyCountAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ApiKeys.CountAsync(cancellationToken);
    }

    public async Task<Result<(string PlainKey, UserApiKey ApiKey)>> CreateApiKeyAsync(
        Guid userId, string name, ApiKeyScope scope, DateTime? expiresAt, CancellationToken cancellationToken = default)
    {
        // Check license limits
        var license = await licenseService.GetActiveLicenseAsync(userId, cancellationToken);
        if (license is null)
        {
            return Result.Fail<(string, UserApiKey)>("No active license found");
        }

        var currentKeyCount = await dbContext.ApiKeys
            .CountAsync(k => k.UserId == userId && !k.IsRevoked, cancellationToken);

        if (currentKeyCount >= license.MaxApiKeys)
        {
            return Result.Fail<(string, UserApiKey)>($"API key limit reached ({license.MaxApiKeys}). Upgrade your plan to create more.");
        }

        var (plainKey, apiKey) = UserApiKey.Create(userId, name, scope, expiresAt);

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok((plainKey, apiKey));
    }

    public async Task<Result> RevokeApiKeyAsync(Guid userId, Guid keyId, CancellationToken cancellationToken = default)
    {
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, cancellationToken);

        if (apiKey is null)
        {
            return Result.Fail("API key not found");
        }

        if (apiKey.IsRevoked)
        {
            return Result.Fail("API key is already revoked");
        }

        apiKey.Revoke("User revoked");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    public async Task<Result> AdminRevokeApiKeyAsync(Guid keyId, CancellationToken cancellationToken = default)
    {
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken);

        if (apiKey is null)
        {
            return Result.Fail("API key not found");
        }

        if (apiKey.IsRevoked)
        {
            return Result.Fail("API key is already revoked");
        }

        apiKey.Revoke("Admin revoked");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    public async Task<Result<(Guid UserId, ApiKeyScope Scope)>> ValidateApiKeyAsync(string plainKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainKey) || !plainKey.StartsWith(KeyPrefix))
        {
            return Result.Fail<(Guid, ApiKeyScope)>("Invalid API key format");
        }

        var keyHash = UserApiKey.HashKey(plainKey);
        var keyPrefix = plainKey[..11];

        var apiKey = await dbContext.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix && k.KeyHash == keyHash, cancellationToken);

        if (apiKey is null)
        {
            return Result.Fail<(Guid, ApiKeyScope)>("Invalid API key");
        }

        var validationResult = apiKey.ValidateForUse(apiKey.User?.IsActive == true, DateTime.UtcNow);
        if (validationResult.IsFailed)
        {
            return Result.Fail<(Guid, ApiKeyScope)>(validationResult.Errors);
        }

        apiKey.MarkUsed(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok((apiKey.UserId, apiKey.Scope));
    }
}
