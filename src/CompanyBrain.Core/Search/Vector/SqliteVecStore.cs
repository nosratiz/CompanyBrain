using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// Local-first vector store backed by SQLite + the sqlite-vec virtual table extension.
/// One row per indexable snippet; cosine distance via <c>vec_distance_cosine</c>.
/// </summary>
public sealed class SqliteVecStore
{
    private readonly string connectionString;
    private readonly int dimensions;
    private readonly ILogger<SqliteVecStore> logger;
    private bool initialized;

    public SqliteVecStore(string databasePath, int dimensions, ILogger<SqliteVecStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Vector dimensions must be > 0.");
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        this.dimensions = dimensions;
        this.logger = logger;
    }

    public string ConnectionString => connectionString;
    public int Dimensions => dimensions;

    /// <summary>
    /// Loads sqlite-vec, creates the schema (idempotent), and verifies the vec0 table dimension.
    /// Migrates legacy single-row-per-document schemas to per-chunk indexing.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);

        // Migrate legacy schema that had resource_uri UNIQUE (one row per document).
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(documents)";
            var columns = new List<string>();
            await using var reader = await pragma.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                columns.Add(reader.GetString(1));
            }

            if (columns.Count > 0 && !columns.Contains("chunk_index"))
            {
                await using var drop = connection.CreateCommand();
                drop.CommandText = "DROP TABLE IF EXISTS document_vectors; DROP TABLE IF EXISTS documents;";
                await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning("Vector store schema migrated to per-chunk indexing. All documents must be re-indexed.");
            }
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                CREATE TABLE IF NOT EXISTS documents (
                    doc_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    resource_uri     TEXT NOT NULL,
                    chunk_index      INTEGER NOT NULL DEFAULT 0,
                    collection_id    TEXT NOT NULL,
                    redacted_snippet TEXT NOT NULL,
                    content_hash     TEXT NOT NULL,
                    model            TEXT NOT NULL,
                    embedded_utc     TEXT NOT NULL,
                    UNIQUE(resource_uri, chunk_index)
                );
                CREATE INDEX IF NOT EXISTS ix_documents_collection ON documents(collection_id);
                CREATE INDEX IF NOT EXISTS ix_documents_uri ON documents(resource_uri);

                CREATE VIRTUAL TABLE IF NOT EXISTS document_vectors USING vec0(
                    embedding float[{dimensions}]
                );
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        initialized = true;
        logger.LogInformation("DeepRoot vector store ready at {Path} ({Dim} dims).", connection.DataSource, dimensions);
    }

    /// <summary>
    /// Looks up the stored content hash for a resource (for delta-update decisions).
    /// Uses chunk 0 as the authoritative hash row.
    /// </summary>
    public async Task<string?> GetStoredHashAsync(string resourceUri, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT content_hash FROM documents WHERE resource_uri = $uri AND chunk_index = 0";
        cmd.Parameters.AddWithValue("$uri", resourceUri);
        return await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    /// <summary>
    /// Inserts or replaces a single chunk vector + metadata.
    /// Use <paramref name="chunkIndex"/> to store multiple chunks per document.
    /// The vec0 row is rebuilt because sqlite-vec virtual tables don't support
    /// in-place updates of the embedding column.
    /// </summary>
    public async Task UpsertAsync(
        string resourceUri,
        int chunkIndex,
        string collectionId,
        string redactedSnippet,
        string contentHash,
        string model,
        float[] vector,
        CancellationToken cancellationToken)
    {
        if (vector.Length != dimensions)
        {
            throw new ArgumentException(
                $"Vector length {vector.Length} does not match store dimensions {dimensions}.", nameof(vector));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var blob = EmbeddingCache.ToBlob(vector);
        var nowUtc = DateTime.UtcNow.ToString("O");

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        long docId;
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = tx;
            existing.CommandText = "SELECT doc_id FROM documents WHERE resource_uri = $uri AND chunk_index = $chunk";
            existing.Parameters.AddWithValue("$uri", resourceUri);
            existing.Parameters.AddWithValue("$chunk", chunkIndex);
            var scalar = await existing.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            docId = scalar is long l ? l : 0L;
        }

        if (docId == 0)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO documents (resource_uri, chunk_index, collection_id, redacted_snippet, content_hash, model, embedded_utc)
                VALUES ($uri, $chunk, $col, $snippet, $hash, $model, $now);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$uri", resourceUri);
            insert.Parameters.AddWithValue("$chunk", chunkIndex);
            insert.Parameters.AddWithValue("$col", collectionId);
            insert.Parameters.AddWithValue("$snippet", redactedSnippet);
            insert.Parameters.AddWithValue("$hash", contentHash);
            insert.Parameters.AddWithValue("$model", model);
            insert.Parameters.AddWithValue("$now", nowUtc);
            docId = (long)(await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
        else
        {
            await using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = """
                UPDATE documents
                SET collection_id = $col,
                    redacted_snippet = $snippet,
                    content_hash = $hash,
                    model = $model,
                    embedded_utc = $now
                WHERE doc_id = $id;
                """;
            update.Parameters.AddWithValue("$col", collectionId);
            update.Parameters.AddWithValue("$snippet", redactedSnippet);
            update.Parameters.AddWithValue("$hash", contentHash);
            update.Parameters.AddWithValue("$model", model);
            update.Parameters.AddWithValue("$now", nowUtc);
            update.Parameters.AddWithValue("$id", docId);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await using var deleteVec = connection.CreateCommand();
            deleteVec.Transaction = tx;
            deleteVec.CommandText = "DELETE FROM document_vectors WHERE rowid = $id";
            deleteVec.Parameters.AddWithValue("$id", docId);
            await deleteVec.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var insertVec = connection.CreateCommand())
        {
            insertVec.Transaction = tx;
            insertVec.CommandText = "INSERT INTO document_vectors(rowid, embedding) VALUES ($id, $vec)";
            insertVec.Parameters.AddWithValue("$id", docId);
            insertVec.Parameters.AddWithValue("$vec", blob);
            await insertVec.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all chunks (and their vectors) for the given resource URI.
    /// </summary>
    public async Task DeleteByUriAsync(string resourceUri, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var docIds = new List<long>();
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = "SELECT doc_id FROM documents WHERE resource_uri = $uri";
            find.Parameters.AddWithValue("$uri", resourceUri);
            await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                docIds.Add(reader.GetInt64(0));
            }
        }

        if (docIds.Count == 0)
        {
            return;
        }

        foreach (var docId in docIds)
        {
            await using var deleteVec = connection.CreateCommand();
            deleteVec.Transaction = tx;
            deleteVec.CommandText = "DELETE FROM document_vectors WHERE rowid = $id";
            deleteVec.Parameters.AddWithValue("$id", docId);
            await deleteVec.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var deleteDoc = connection.CreateCommand())
        {
            deleteDoc.Transaction = tx;
            deleteDoc.CommandText = "DELETE FROM documents WHERE resource_uri = $uri";
            deleteDoc.Parameters.AddWithValue("$uri", resourceUri);
            await deleteDoc.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Top-K cosine search over the vec0 virtual table.
    /// Returns the redacted snippet (no original content read) so the agent never leaks raw files.
    /// </summary>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchTopKAsync(
        float[] queryVector,
        int k,
        string? collectionId,
        CancellationToken cancellationToken)
    {
        if (queryVector.Length != dimensions)
        {
            throw new ArgumentException(
                $"Query vector length {queryVector.Length} does not match store dimensions {dimensions}.", nameof(queryVector));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var clampedK = Math.Clamp(k, 1, 50);
        var blob = EmbeddingCache.ToBlob(queryVector);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();

        // KNN syntax: filter the vec0 virtual table with `MATCH` + `k = ?`,
        // then join to documents for the snippet payload.
        var collectionFilter = string.IsNullOrWhiteSpace(collectionId) ? string.Empty : "AND d.collection_id = $col";
        cmd.CommandText = $"""
            SELECT d.resource_uri, d.collection_id, d.redacted_snippet, v.distance
            FROM document_vectors v
            JOIN documents d ON d.doc_id = v.rowid
            WHERE v.embedding MATCH $vec
              AND k = $k
              {collectionFilter}
            ORDER BY v.distance;
            """;
        cmd.Parameters.AddWithValue("$vec", blob);
        cmd.Parameters.AddWithValue("$k", clampedK);
        if (!string.IsNullOrWhiteSpace(collectionId))
        {
            cmd.Parameters.AddWithValue("$col", collectionId);
        }

        var rows = new List<VectorSearchResult>(clampedK);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new VectorSearchResult(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3)));
        }
        return rows;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        connection.EnableExtensions(true);
        // sqlite-vec NuGet ships native binaries; SqliteVecLoader resolves the platform path.
        connection.LoadExtension(SqliteVecLoader.GetExtensionPath());
        return connection;
    }
}
