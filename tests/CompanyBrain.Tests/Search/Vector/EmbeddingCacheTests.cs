using CompanyBrain.Search.Vector;

namespace CompanyBrain.Tests.Search.Vector;

public sealed class EmbeddingCacheTests : IDisposable
{
    private readonly string dbPath;
    private readonly EmbeddingCache cache;

    public EmbeddingCacheTests()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"deeproot-cache-{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        cache = new EmbeddingCache(connectionString);
        cache.EnsureSchemaAsync(default).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Set_then_get_round_trips_vector()
    {
        var vector = new[] { 0.1f, 0.2f, -0.3f, 0.4f };
        await cache.SetAsync("hello", "test-model", 4, vector, default);

        var fetched = await cache.TryGetAsync("hello", "test-model", 4, default);

        Assert.NotNull(fetched);
        Assert.Equal(vector, fetched);
    }

    [Fact]
    public async Task Get_returns_null_on_miss()
    {
        var result = await cache.TryGetAsync("never-stored", "test-model", 4, default);
        Assert.Null(result);
    }

    [Fact]
    public async Task Different_model_or_dimensions_isolates_cache_entries()
    {
        var vector = new[] { 1f, 2f, 3f, 4f };
        await cache.SetAsync("text", "model-a", 4, vector, default);

        Assert.Null(await cache.TryGetAsync("text", "model-b", 4, default));
        Assert.Null(await cache.TryGetAsync("text", "model-a", 8, default));
    }

    [Fact]
    public async Task Set_rejects_dimension_mismatch()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => cache.SetAsync("x", "m", 8, new[] { 1f, 2f }, default));
    }

    [Fact]
    public void Compute_key_is_deterministic_and_dimension_aware()
    {
        var k1 = EmbeddingCache.ComputeKey("text", "m", 4);
        var k2 = EmbeddingCache.ComputeKey("text", "m", 4);
        var k3 = EmbeddingCache.ComputeKey("text", "m", 8);

        Assert.Equal(k1, k2);
        Assert.NotEqual(k1, k3);
    }
}
