using CompanyBrain.Dashboard.Services;
using CompanyBrain.Search.Vector;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Database-backed <see cref="IEmbeddingOptionsAccessor"/>. Caches the decrypted snapshot for a
/// short window so high-frequency MCP/search calls don't hit the DB on every request, while still
/// letting Settings UI updates take effect quickly.
/// </summary>
public sealed class DatabaseEmbeddingOptionsAccessor : IEmbeddingOptionsAccessor
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(15);

    private readonly DeepRootSettingsService settingsService;
    private readonly Lock gate = new();

    private EmbeddingOptions? cached;
    private DateTime cachedUntilUtc = DateTime.MinValue;

    public DatabaseEmbeddingOptionsAccessor(DeepRootSettingsService settingsService)
    {
        this.settingsService = settingsService;
    }

    public EmbeddingOptions GetCurrent()
    {
        lock (gate)
        {
            if (cached is not null && DateTime.UtcNow < cachedUntilUtc)
            {
                return cached;
            }
        }

        // Block synchronously — accessor contract is sync. The DB call is local SQLite + a single
        // singleton row lookup, so this is very cheap. The 15-second cache prevents thrash.
        var snapshot = settingsService.GetEmbeddingOptionsAsync().GetAwaiter().GetResult();

        lock (gate)
        {
            cached = snapshot;
            cachedUntilUtc = DateTime.UtcNow.Add(CacheLifetime);
            return snapshot;
        }
    }

    /// <summary>
    /// Drops the cached snapshot. <see cref="DeepRootSettingsService.UpdateAsync"/> already
    /// triggers a provider rebuild via <see cref="EmbeddingProviderFactory.Reload"/>, but consumers
    /// can call this to force the next read to hit the database.
    /// </summary>
    public void Invalidate()
    {
        lock (gate)
        {
            cached = null;
            cachedUntilUtc = DateTime.MinValue;
        }
    }
}
