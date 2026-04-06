using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Domain;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.MultiTenant.Services;

public sealed class ApiKeyService(
    TenantDbContext dbContext,
    ILogger<ApiKeyService> logger)
{
    /// <summary>
    /// Creates a new API key for a tenant.
    /// Returns the plain key (show once!) and the saved entity.
    /// </summary>
    public async Task<Result<(string PlainKey, ApiKey KeyEntity)>> CreateApiKeyAsync(
        Guid tenantId,
        string name,
        ApiKeyScope scope = ApiKeyScope.ReadOnly,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        // Verify tenant exists and check limits
        var tenant = await dbContext.Tenants
            .Include(t => t.ApiKeys.Where(k => !k.IsRevoked))
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Fail<(string, ApiKey)>($"Tenant {tenantId} not found.");
        }

        var createKeyResult = tenant.EnsureCanCreateApiKey(tenant.ApiKeys.Count);
        if (createKeyResult.IsFailed)
        {
            return Result.Fail<(string, ApiKey)>(createKeyResult.Errors);
        }

        var (plainKey, entity) = ApiKey.Generate(tenantId, name, scope, expiresAt);

        dbContext.ApiKeys.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created API key '{Name}' (prefix: {KeyPrefix}) for tenant {TenantId}.",
            name, entity.KeyPrefix, tenantId);

        return Result.Ok((plainKey, entity));
    }

    /// <summary>
    /// Validates an API key and returns the associated tenant ID if valid.
    /// </summary>
    public async Task<Result<ApiKeyValidationResult>> ValidateApiKeyAsync(
        string plainKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainKey) || !plainKey.StartsWith("cb_"))
        {
            return Result.Fail<ApiKeyValidationResult>("Invalid API key format.");
        }

        var keyHash = ApiKey.HashKey(plainKey);

        var apiKey = await dbContext.ApiKeys
            .Include(k => k.Tenant)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);

        if (apiKey is null)
        {
            logger.LogWarning("API key validation failed: key not found.");
            return Result.Fail<ApiKeyValidationResult>("Invalid API key.");
        }

        var validation = apiKey.ValidateForUse(apiKey.Tenant?.Status, DateTime.UtcNow);
        if (validation.IsFailed)
        {
            logger.LogWarning("API key {KeyPrefix} validation failed: {Reason}", apiKey.KeyPrefix, validation.Errors.First().Message);
            return Result.Fail<ApiKeyValidationResult>(validation.Errors);
        }

        apiKey.MarkUsed(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogDebug("API key {KeyPrefix} validated for tenant {TenantId}.", apiKey.KeyPrefix, apiKey.TenantId);

        return Result.Ok(new ApiKeyValidationResult(
            apiKey.TenantId,
            apiKey.Tenant!.Slug,
            apiKey.Scope,
            apiKey.RequestsPerMinute,
            apiKey.RequestsPerDay));
    }

    /// <summary>
    /// Lists API keys for a tenant (without exposing the actual keys).
    /// </summary>
    public async Task<IReadOnlyList<ApiKey>> ListApiKeysAsync(
        Guid tenantId,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ApiKeys.Where(k => k.TenantId == tenantId);

        if (!includeRevoked)
        {
            query = query.Where(k => !k.IsRevoked);
        }

        return await query.OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Revokes an API key.
    /// </summary>
    public async Task<Result> RevokeApiKeyAsync(
        Guid tenantId,
        Guid keyId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, cancellationToken);

        if (apiKey is null)
        {
            return Result.Fail("API key not found.");
        }

        if (apiKey.IsRevoked)
        {
            return Result.Fail("API key is already revoked.");
        }

        apiKey.Revoke(reason);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Revoked API key {KeyPrefix} for tenant {TenantId}. Reason: {Reason}",
            apiKey.KeyPrefix, tenantId, reason ?? "Not specified");

        return Result.Ok();
    }

    /// <summary>
    /// Regenerates an API key (revokes old one and creates new with same settings).
    /// </summary>
    public async Task<Result<(string PlainKey, ApiKey KeyEntity)>> RegenerateApiKeyAsync(
        Guid tenantId,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var oldKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, cancellationToken);

        if (oldKey is null)
        {
            return Result.Fail<(string, ApiKey)>("API key not found.");
        }

        oldKey.Revoke("Regenerated");
        var (plainKey, newEntity) = ApiKey.Regenerate(oldKey);

        dbContext.ApiKeys.Add(newEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Regenerated API key {OldPrefix} -> {NewPrefix} for tenant {TenantId}.",
            oldKey.KeyPrefix, newEntity.KeyPrefix, tenantId);

        return Result.Ok((plainKey, newEntity));
    }
}

public sealed record ApiKeyValidationResult(
    Guid TenantId,
    string TenantSlug,
    ApiKeyScope Scope,
    int RequestsPerMinute,
    int RequestsPerDay);
