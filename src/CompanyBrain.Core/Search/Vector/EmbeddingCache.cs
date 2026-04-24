using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// SHA256-keyed embedding cache backed by the same SQLite file as the vector store.
/// Avoids re-paying providers for identical inputs across runs.
/// </summary>
public sealed class EmbeddingCache
{
    private readonly string connectionString;
    // Lazy schema init — ensures the table exists exactly once per instance, even if
    // EnsureSchemaAsync was never called explicitly during startup.
    private Task? _schemaInit;

    public EmbeddingCache(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await InitSchemaOnceAsync().ConfigureAwait(false);
    }

    private Task InitSchemaOnceAsync()
    {
        if (_schemaInit is null)
        {
            // Use Interlocked to avoid a double-init race.
            var t = CreateSchemaAsync();
            Interlocked.CompareExchange(ref _schemaInit, t, null);
        }
        return _schemaInit!;
    }

    private async Task CreateSchemaAsync()
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS embedding_cache (
                cache_key   TEXT PRIMARY KEY,
                model       TEXT NOT NULL,
                dimensions  INTEGER NOT NULL,
                vector      BLOB NOT NULL,
                created_utc TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<float[]?> TryGetAsync(string text, string model, int dimensions, CancellationToken cancellationToken)
    {
        await InitSchemaOnceAsync().ConfigureAwait(false);

        var key = ComputeKey(text, model, dimensions);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT vector FROM embedding_cache WHERE cache_key = $key";
        cmd.Parameters.AddWithValue("$key", key);

        var blob = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return blob is byte[] bytes ? FromBlob(bytes) : null;
    }

    public async Task SetAsync(string text, string model, int dimensions, float[] vector, CancellationToken cancellationToken)
    {
        if (vector.Length != dimensions)
        {
            throw new ArgumentException($"Vector length {vector.Length} does not match expected dimensions {dimensions}.", nameof(vector));
        }

        await InitSchemaOnceAsync().ConfigureAwait(false);

        var key = ComputeKey(text, model, dimensions);
        var blob = ToBlob(vector);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO embedding_cache (cache_key, model, dimensions, vector, created_utc)
            VALUES ($key, $model, $dim, $vec, $now)
            ON CONFLICT(cache_key) DO UPDATE SET
                vector = excluded.vector,
                created_utc = excluded.created_utc;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$model", model);
        cmd.Parameters.AddWithValue("$dim", dimensions);
        cmd.Parameters.AddWithValue("$vec", blob);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static string ComputeKey(string text, string model, int dimensions)
    {
        var payload = $"{model}|{dimensions}|{text ?? string.Empty}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    internal static byte[] ToBlob(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), vector[i]);
        }
        return bytes;
    }

    internal static float[] FromBlob(byte[] blob)
    {
        var floats = new float[blob.Length / sizeof(float)];
        for (var i = 0; i < floats.Length; i++)
        {
            floats[i] = BinaryPrimitives.ReadSingleLittleEndian(blob.AsSpan(i * sizeof(float)));
        }
        return floats;
    }
}
