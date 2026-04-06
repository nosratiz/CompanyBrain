using CompanyBrain.MultiTenant.Abstractions;
using CompanyBrain.Services;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.MultiTenant.Services;

/// <summary>
/// Factory that creates tenant-scoped KnowledgeStore instances.
/// Each tenant gets its own isolated storage directory.
/// </summary>
public sealed class TenantKnowledgeStoreFactory(
    string baseStoragePath,
    ITenantContext tenantContext,
    ILoggerFactory loggerFactory)
{
    private readonly Dictionary<Guid, KnowledgeStore> _stores = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Gets or creates a KnowledgeStore for the current tenant.
    /// </summary>
    public async Task<KnowledgeStore> GetStoreAsync(CancellationToken cancellationToken = default)
    {
        if (!tenantContext.HasTenant)
        {
            throw new InvalidOperationException("No tenant context available. Ensure API key authentication middleware is configured.");
        }

        return await GetStoreForTenantAsync(tenantContext.TenantId!.Value, tenantContext.TenantSlug!, cancellationToken);
    }

    /// <summary>
    /// Gets or creates a KnowledgeStore for a specific tenant.
    /// </summary>
    public async Task<KnowledgeStore> GetStoreForTenantAsync(Guid tenantId, string tenantSlug, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        KnowledgeStore store;
        lock (_lock)
        {
            if (_stores.TryGetValue(tenantId, out var existingStore))
            {
                store = existingStore;
            }
            else
            {
                var tenantPath = Path.Combine(baseStoragePath, "tenants", tenantSlug);
                store = new KnowledgeStore(
                    tenantPath,
                    loggerFactory.CreateLogger<KnowledgeStore>());

                _stores[tenantId] = store;
            }
        }

        await store.EnsureFolderExistsAsync(cancellationToken);
        return store;
    }

    /// <summary>
    /// Gets storage statistics for a tenant.
    /// </summary>
    public Task<TenantStorageStats> GetStorageStatsAsync(Guid tenantId, string tenantSlug, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tenantPath = Path.Combine(baseStoragePath, "tenants", tenantSlug);

        if (!Directory.Exists(tenantPath))
        {
            return Task.FromResult(new TenantStorageStats(0, 0));
        }

        var files = Directory.GetFiles(tenantPath, "*.md", SearchOption.TopDirectoryOnly);
        var totalSize = files.Sum(f => new FileInfo(f).Length);

        return Task.FromResult(new TenantStorageStats(files.Length, totalSize));
    }
}

public sealed record TenantStorageStats(int DocumentCount, long TotalBytes)
{
    public string FormattedSize => TotalBytes switch
    {
        < 1024 => $"{TotalBytes} B",
        < 1024 * 1024 => $"{TotalBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{TotalBytes / (1024.0 * 1024.0):F1} MB",
        _ => $"{TotalBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };
}
