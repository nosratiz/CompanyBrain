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
    public KnowledgeStore GetStore()
    {
        if (!tenantContext.HasTenant)
        {
            throw new InvalidOperationException("No tenant context available. Ensure API key authentication middleware is configured.");
        }

        return GetStoreForTenant(tenantContext.TenantId!.Value, tenantContext.TenantSlug!);
    }

    /// <summary>
    /// Gets or creates a KnowledgeStore for a specific tenant.
    /// </summary>
    public KnowledgeStore GetStoreForTenant(Guid tenantId, string tenantSlug)
    {
        lock (_lock)
        {
            if (_stores.TryGetValue(tenantId, out var existingStore))
            {
                return existingStore;
            }

            var tenantPath = Path.Combine(baseStoragePath, "tenants", tenantSlug);
            var store = new KnowledgeStore(
                tenantPath,
                loggerFactory.CreateLogger<KnowledgeStore>());

            store.EnsureFolderExists();
            _stores[tenantId] = store;

            return store;
        }
    }

    /// <summary>
    /// Gets storage statistics for a tenant.
    /// </summary>
    public TenantStorageStats GetStorageStats(Guid tenantId, string tenantSlug)
    {
        var tenantPath = Path.Combine(baseStoragePath, "tenants", tenantSlug);

        if (!Directory.Exists(tenantPath))
        {
            return new TenantStorageStats(0, 0);
        }

        var files = Directory.GetFiles(tenantPath, "*.md", SearchOption.TopDirectoryOnly);
        var totalSize = files.Sum(f => new FileInfo(f).Length);

        return new TenantStorageStats(files.Length, totalSize);
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
