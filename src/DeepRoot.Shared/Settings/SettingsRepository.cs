using Microsoft.Data.Sqlite;

namespace DeepRoot.Shared.Settings;

/// <summary>
/// Tiny accessor for the local <c>SystemSettings</c> table.
/// One row per (Key, Value); created lazily on first read/write.
/// Replaces appsettings.json for the DeepRoot desktop shell.
/// </summary>
public sealed class SettingsRepository
{
    private readonly SqliteConnection _connection;
    private bool _initialised;
    private readonly object _gate = new();

    public SettingsRepository(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        EnsureSchema();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM SystemSettings WHERE Key = $key LIMIT 1;";
        cmd.Parameters.AddWithValue("$key", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is null or DBNull ? null : (string)result;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        EnsureSchema();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO SystemSettings(Key, Value, UpdatedUtc)
            VALUES ($key, $value, $ts)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedUtc = excluded.UpdatedUtc;
            """;
        cmd.Parameters.AddWithValue("$key",   key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$ts",    DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private void EnsureSchema()
    {
        if (_initialised) return;
        lock (_gate)
        {
            if (_initialised) return;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS SystemSettings (
                    Key        TEXT PRIMARY KEY,
                    Value      TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
            _initialised = true;
        }
    }
}
