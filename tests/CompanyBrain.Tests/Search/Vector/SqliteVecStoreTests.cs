using CompanyBrain.Search.Vector;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Tests.Search.Vector;

public sealed class SqliteVecStoreTests : IDisposable
{
    private readonly string dbPath;

    public SqliteVecStoreTests()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"deeproot-vec-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Upsert_then_top_k_returns_inserted_snippet()
    {
        var store = new SqliteVecStore(dbPath, dimensions: 4, NullLogger<SqliteVecStore>.Instance);

        try
        {
            await store.InitializeAsync(default);
        }
        catch (Exception ex) when (IsNativeLoadFailure(ex))
        {
            // sqlite-vec native lib not present in this runtime; skip rather than fail.
            return;
        }

        await store.UpsertAsync(
            "knowledge://General/policy.md",
            "General",
            "Remote work is allowed.",
            contentHash: "h1",
            model: "test-model",
            vector: [1f, 0f, 0f, 0f],
            cancellationToken: default);

        await store.UpsertAsync(
            "knowledge://General/lunch.md",
            "General",
            "Lunch is at noon.",
            contentHash: "h2",
            model: "test-model",
            vector: [0f, 1f, 0f, 0f],
            cancellationToken: default);

        var hits = await store.SearchTopKAsync([0.95f, 0.05f, 0f, 0f], k: 1, collectionId: null, default);

        Assert.Single(hits);
        Assert.Equal("knowledge://General/policy.md", hits[0].ResourceUri);
        Assert.Equal("Remote work is allowed.", hits[0].RedactedSnippet);
    }

    [Fact]
    public async Task Get_stored_hash_returns_persisted_value()
    {
        var store = new SqliteVecStore(dbPath, dimensions: 4, NullLogger<SqliteVecStore>.Instance);

        try
        {
            await store.InitializeAsync(default);
        }
        catch (Exception ex) when (IsNativeLoadFailure(ex))
        {
            return;
        }

        await store.UpsertAsync(
            "knowledge://General/x.md",
            "General",
            "snippet",
            contentHash: "abc123",
            model: "test-model",
            vector: [1f, 0f, 0f, 0f],
            cancellationToken: default);

        var hash = await store.GetStoredHashAsync("knowledge://General/x.md", default);
        Assert.Equal("abc123", hash);
    }

    private static bool IsNativeLoadFailure(Exception ex)
        => ex is Microsoft.Data.Sqlite.SqliteException sqliteEx
           && (sqliteEx.Message.Contains("vec0", StringComparison.OrdinalIgnoreCase)
               || sqliteEx.Message.Contains("LoadExtension", StringComparison.OrdinalIgnoreCase)
               || sqliteEx.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase));
}
