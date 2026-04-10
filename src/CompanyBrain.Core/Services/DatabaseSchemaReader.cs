using System.Data;
using System.Data.Common;
using CompanyBrain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Npgsql;

namespace CompanyBrain.Services;

public sealed class DatabaseSchemaReader(ILogger<DatabaseSchemaReader>? logger = null)
{
    private readonly ILogger<DatabaseSchemaReader> _logger = logger ?? NullLogger<DatabaseSchemaReader>.Instance;

    public async Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString, DatabaseProvider provider, CancellationToken ct = default)
    {
        await using var connection = CreateConnection(connectionString, provider);
        await connection.OpenAsync(ct);

        var schema = new DatabaseSchema
        {
            DatabaseName = connection.Database,
            ServerName = connection.DataSource,
            Provider = provider,
            ExportedAtUtc = DateTime.UtcNow,
        };

        _logger.LogInformation("Reading {Provider} database schema for '{Database}' on '{Server}'.",
            provider, schema.DatabaseName, schema.ServerName);

        schema.Tables = await ReadTablesAsync(connection, provider, ct);
        schema.Views = await ReadViewsAsync(connection, provider, ct);
        schema.StoredProcedures = await ReadStoredProceduresAsync(connection, provider, ct);
        schema.Functions = await ReadFunctionsAsync(connection, provider, ct);
        schema.Triggers = await ReadTriggersAsync(connection, provider, ct);
        schema.ForeignKeys = await ReadForeignKeysAsync(connection, provider, ct);

        _logger.LogInformation(
            "Schema read complete: {Tables} tables, {Views} views, {Procs} procedures, {Funcs} functions, {Triggers} triggers, {FKs} foreign keys.",
            schema.Tables.Count, schema.Views.Count, schema.StoredProcedures.Count,
            schema.Functions.Count, schema.Triggers.Count, schema.ForeignKeys.Count);

        return schema;
    }

    private static DbConnection CreateConnection(string connectionString, DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.SqlServer => new SqlConnection(connectionString),
            DatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
            DatabaseProvider.MySql => new MySqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported database provider."),
        };

    private static DbCommand CreateCommand(string sql, DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    // ── Helper to safely read values across providers ────────────────

    private static string GetString(DbDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? "" : r.GetString(ordinal);

    private static string? GetNullableString(DbDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private static int? GetNullableInt(DbDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? null : Convert.ToInt32(r.GetValue(ordinal));

    private static bool GetBool(DbDataReader r, int ordinal) =>
        !r.IsDBNull(ordinal) && Convert.ToBoolean(r.GetValue(ordinal));

    private static int GetInt(DbDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? 0 : Convert.ToInt32(r.GetValue(ordinal));

    // ═══════════════════════════════════════════════════════════════
    //  TABLES
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<TableSchema>> ReadTablesAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var tables = new List<TableSchema>();

        var tablesSql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, t.name
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE t.is_ms_shipped = 0
                ORDER BY s.name, t.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY table_schema, table_name
                """,
            DatabaseProvider.MySql => $"""
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema = DATABASE()
                ORDER BY table_schema, table_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        await using (var cmd = CreateCommand(tablesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                tables.Add(new TableSchema { SchemaName = GetString(reader, 0), TableName = GetString(reader, 1) });
        }

        // ── Columns ──
        var columnsSql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, t.name, c.name, tp.name,
                       c.max_length, c.precision, c.scale, c.is_nullable,
                       CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END,
                       c.is_identity, dc.definition
                FROM sys.columns c
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                LEFT JOIN (
                    SELECT ic.object_id, ic.column_id
                    FROM sys.index_columns ic
                    INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    WHERE i.is_primary_key = 1
                ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
                LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                WHERE t.is_ms_shipped = 0
                ORDER BY s.name, t.name, c.column_id
                """,
            DatabaseProvider.PostgreSql => """
                SELECT c.table_schema, c.table_name, c.column_name, c.data_type,
                       c.character_maximum_length, c.numeric_precision, c.numeric_scale,
                       CASE WHEN c.is_nullable = 'YES' THEN true ELSE false END,
                       CASE WHEN tc.constraint_type = 'PRIMARY KEY' THEN 1 ELSE 0 END,
                       CASE WHEN c.column_default LIKE 'nextval%' THEN true ELSE false END,
                       c.column_default
                FROM information_schema.columns c
                LEFT JOIN information_schema.key_column_usage kcu
                    ON c.table_schema = kcu.table_schema AND c.table_name = kcu.table_name AND c.column_name = kcu.column_name
                LEFT JOIN information_schema.table_constraints tc
                    ON kcu.constraint_name = tc.constraint_name AND tc.constraint_type = 'PRIMARY KEY' AND tc.table_schema = kcu.table_schema
                WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY c.table_schema, c.table_name, c.ordinal_position
                """,
            DatabaseProvider.MySql => """
                SELECT c.table_schema, c.table_name, c.column_name, c.data_type,
                       c.character_maximum_length, c.numeric_precision, c.numeric_scale,
                       CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END,
                       CASE WHEN c.column_key = 'PRI' THEN 1 ELSE 0 END,
                       CASE WHEN c.extra LIKE '%auto_increment%' THEN 1 ELSE 0 END,
                       c.column_default
                FROM information_schema.columns c
                WHERE c.table_schema = DATABASE()
                ORDER BY c.table_schema, c.table_name, c.ordinal_position
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var columnsByTable = new Dictionary<string, List<ColumnSchema>>();
        await using (var cmd = CreateCommand(columnsSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var key = $"{GetString(reader, 0)}.{GetString(reader, 1)}";
                if (!columnsByTable.TryGetValue(key, out var cols))
                {
                    cols = [];
                    columnsByTable[key] = cols;
                }
                cols.Add(new ColumnSchema
                {
                    ColumnName = GetString(reader, 2),
                    DataType = GetString(reader, 3),
                    MaxLength = GetNullableInt(reader, 4),
                    Precision = GetNullableInt(reader, 5),
                    Scale = GetNullableInt(reader, 6),
                    IsNullable = GetBool(reader, 7),
                    IsPrimaryKey = GetInt(reader, 8) == 1,
                    IsIdentity = GetBool(reader, 9),
                    DefaultValue = GetNullableString(reader, 10),
                });
            }
        }

        // ── Indexes ──
        var indexesSql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, t.name, i.name, i.is_unique, i.type_desc, c.name
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE t.is_ms_shipped = 0 AND i.name IS NOT NULL
                ORDER BY s.name, t.name, i.name, ic.key_ordinal
                """,
            DatabaseProvider.PostgreSql => """
                SELECT schemaname, tablename, indexname,
                       indisunique::int,
                       CASE WHEN amname = 'btree' THEN 'NONCLUSTERED' ELSE amname END,
                       a.attname
                FROM pg_indexes pi
                JOIN pg_class c ON c.relname = pi.indexname
                JOIN pg_index ix ON ix.indexrelid = c.oid
                JOIN pg_attribute a ON a.attrelid = ix.indrelid AND a.attnum = ANY(ix.indkey)
                JOIN pg_am am ON am.oid = c.relam
                WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
                ORDER BY schemaname, tablename, indexname, a.attnum
                """,
            DatabaseProvider.MySql => """
                SELECT table_schema, table_name, index_name,
                       CASE WHEN non_unique = 0 THEN 1 ELSE 0 END,
                       index_type, column_name
                FROM information_schema.statistics
                WHERE table_schema = DATABASE()
                ORDER BY table_schema, table_name, index_name, seq_in_index
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var indexesByTable = new Dictionary<string, Dictionary<string, IndexSchema>>();
        await using (var cmd = CreateCommand(indexesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var tableKey = $"{GetString(reader, 0)}.{GetString(reader, 1)}";
                var indexName = GetString(reader, 2);
                if (!indexesByTable.TryGetValue(tableKey, out var indexes))
                {
                    indexes = new Dictionary<string, IndexSchema>();
                    indexesByTable[tableKey] = indexes;
                }
                if (!indexes.TryGetValue(indexName, out var idx))
                {
                    idx = new IndexSchema
                    {
                        IndexName = indexName,
                        IsUnique = GetBool(reader, 3),
                        IsClustered = GetString(reader, 4) == "CLUSTERED",
                    };
                    indexes[indexName] = idx;
                }
                idx.Columns.Add(GetString(reader, 5));
            }
        }

        foreach (var table in tables)
        {
            var key = $"{table.SchemaName}.{table.TableName}";
            if (columnsByTable.TryGetValue(key, out var cols))
                table.Columns = cols;
            if (indexesByTable.TryGetValue(key, out var indexes))
                table.Indexes = indexes.Values.ToList();
        }

        return tables;
    }

    // ═══════════════════════════════════════════════════════════════
    //  VIEWS
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<ViewSchema>> ReadViewsAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var sql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, v.name, m.definition
                FROM sys.views v
                INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
                LEFT JOIN sys.sql_modules m ON v.object_id = m.object_id
                WHERE v.is_ms_shipped = 0
                ORDER BY s.name, v.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT table_schema, table_name, view_definition
                FROM information_schema.views
                WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY table_schema, table_name
                """,
            DatabaseProvider.MySql => """
                SELECT table_schema, table_name, view_definition
                FROM information_schema.views
                WHERE table_schema = DATABASE()
                ORDER BY table_schema, table_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var views = new List<ViewSchema>();
        await using (var cmd = CreateCommand(sql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                views.Add(new ViewSchema
                {
                    SchemaName = GetString(reader, 0),
                    ViewName = GetString(reader, 1),
                    Definition = GetNullableString(reader, 2),
                });
            }
        }

        // ── View columns ──
        var colsSql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, v.name, c.name, tp.name,
                       c.max_length, c.precision, c.scale, c.is_nullable
                FROM sys.columns c
                INNER JOIN sys.views v ON c.object_id = v.object_id
                INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
                INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                WHERE v.is_ms_shipped = 0
                ORDER BY s.name, v.name, c.column_id
                """,
            DatabaseProvider.PostgreSql => """
                SELECT c.table_schema, c.table_name, c.column_name, c.data_type,
                       c.character_maximum_length, c.numeric_precision, c.numeric_scale,
                       CASE WHEN c.is_nullable = 'YES' THEN true ELSE false END
                FROM information_schema.columns c
                INNER JOIN information_schema.views v
                    ON c.table_schema = v.table_schema AND c.table_name = v.table_name
                WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY c.table_schema, c.table_name, c.ordinal_position
                """,
            DatabaseProvider.MySql => """
                SELECT c.table_schema, c.table_name, c.column_name, c.data_type,
                       c.character_maximum_length, c.numeric_precision, c.numeric_scale,
                       CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END
                FROM information_schema.columns c
                INNER JOIN information_schema.views v
                    ON c.table_schema = v.table_schema AND c.table_name = v.table_name
                WHERE c.table_schema = DATABASE()
                ORDER BY c.table_schema, c.table_name, c.ordinal_position
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var columnsByView = new Dictionary<string, List<ColumnSchema>>();
        await using (var cmd = CreateCommand(colsSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var key = $"{GetString(reader, 0)}.{GetString(reader, 1)}";
                if (!columnsByView.TryGetValue(key, out var cols))
                {
                    cols = [];
                    columnsByView[key] = cols;
                }
                cols.Add(new ColumnSchema
                {
                    ColumnName = GetString(reader, 2),
                    DataType = GetString(reader, 3),
                    MaxLength = GetNullableInt(reader, 4),
                    Precision = GetNullableInt(reader, 5),
                    Scale = GetNullableInt(reader, 6),
                    IsNullable = GetBool(reader, 7),
                });
            }
        }

        foreach (var view in views)
        {
            var key = $"{view.SchemaName}.{view.ViewName}";
            if (columnsByView.TryGetValue(key, out var cols))
                view.Columns = cols;
        }

        return views;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STORED PROCEDURES
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<StoredProcedureSchema>> ReadStoredProceduresAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var sql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, p.name, m.definition
                FROM sys.procedures p
                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                LEFT JOIN sys.sql_modules m ON p.object_id = m.object_id
                WHERE p.is_ms_shipped = 0
                ORDER BY s.name, p.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT n.nspname, p.proname, pg_get_functiondef(p.oid)
                FROM pg_proc p
                JOIN pg_namespace n ON p.pronamespace = n.oid
                WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
                  AND p.prokind = 'p'
                ORDER BY n.nspname, p.proname
                """,
            DatabaseProvider.MySql => """
                SELECT routine_schema, routine_name, routine_definition
                FROM information_schema.routines
                WHERE routine_type = 'PROCEDURE'
                  AND routine_schema = DATABASE()
                ORDER BY routine_schema, routine_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var procs = new List<StoredProcedureSchema>();
        await using (var cmd = CreateCommand(sql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                procs.Add(new StoredProcedureSchema
                {
                    SchemaName = GetString(reader, 0),
                    ProcedureName = GetString(reader, 1),
                    Definition = GetNullableString(reader, 2),
                });
            }
        }

        // ── Parameters ──
        var paramsSql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, p.name, pr.name, tp.name, pr.max_length, pr.is_output
                FROM sys.parameters pr
                INNER JOIN sys.procedures p ON pr.object_id = p.object_id
                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                INNER JOIN sys.types tp ON pr.user_type_id = tp.user_type_id
                WHERE p.is_ms_shipped = 0
                ORDER BY s.name, p.name, pr.parameter_id
                """,
            DatabaseProvider.PostgreSql => """
                SELECT n.nspname, p.proname, pa.parameter_name, pa.data_type,
                       pa.character_maximum_length,
                       CASE WHEN pa.parameter_mode = 'OUT' OR pa.parameter_mode = 'INOUT' THEN true ELSE false END
                FROM information_schema.parameters pa
                JOIN information_schema.routines r
                    ON pa.specific_schema = r.specific_schema AND pa.specific_name = r.specific_name
                JOIN pg_proc p ON r.routine_name = p.proname
                JOIN pg_namespace n ON p.pronamespace = n.oid AND n.nspname = r.routine_schema
                WHERE r.routine_type = 'PROCEDURE'
                  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
                  AND pa.parameter_name IS NOT NULL AND pa.parameter_name <> ''
                ORDER BY n.nspname, p.proname, pa.ordinal_position
                """,
            DatabaseProvider.MySql => """
                SELECT specific_schema, specific_name, parameter_name, data_type,
                       character_maximum_length,
                       CASE WHEN parameter_mode = 'OUT' OR parameter_mode = 'INOUT' THEN 1 ELSE 0 END
                FROM information_schema.parameters
                WHERE specific_schema = DATABASE()
                  AND parameter_name IS NOT NULL AND parameter_name <> ''
                  AND routine_type = 'PROCEDURE'
                ORDER BY specific_schema, specific_name, ordinal_position
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var paramsByProc = new Dictionary<string, List<ParameterSchema>>();
        await using (var cmd = CreateCommand(paramsSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var key = $"{GetString(reader, 0)}.{GetString(reader, 1)}";
                if (!paramsByProc.TryGetValue(key, out var pars))
                {
                    pars = [];
                    paramsByProc[key] = pars;
                }
                pars.Add(new ParameterSchema
                {
                    ParameterName = GetString(reader, 2),
                    DataType = GetString(reader, 3),
                    MaxLength = GetNullableInt(reader, 4),
                    IsOutput = GetBool(reader, 5),
                });
            }
        }

        foreach (var proc in procs)
        {
            var key = $"{proc.SchemaName}.{proc.ProcedureName}";
            if (paramsByProc.TryGetValue(key, out var pars))
                proc.Parameters = pars;
        }

        return procs;
    }

    // ═══════════════════════════════════════════════════════════════
    //  FUNCTIONS
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<FunctionSchema>> ReadFunctionsAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var sql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, o.name, o.type_desc, m.definition
                FROM sys.objects o
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                LEFT JOIN sys.sql_modules m ON o.object_id = m.object_id
                WHERE o.type IN ('FN', 'IF', 'TF') AND o.is_ms_shipped = 0
                ORDER BY s.name, o.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT n.nspname, p.proname,
                       CASE p.prokind WHEN 'f' THEN 'SCALAR' WHEN 'a' THEN 'AGGREGATE' WHEN 'w' THEN 'WINDOW' ELSE 'UNKNOWN' END,
                       pg_get_functiondef(p.oid)
                FROM pg_proc p
                JOIN pg_namespace n ON p.pronamespace = n.oid
                WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
                  AND p.prokind IN ('f', 'a', 'w')
                ORDER BY n.nspname, p.proname
                """,
            DatabaseProvider.MySql => """
                SELECT routine_schema, routine_name, 'FUNCTION', routine_definition
                FROM information_schema.routines
                WHERE routine_type = 'FUNCTION' AND routine_schema = DATABASE()
                ORDER BY routine_schema, routine_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var functions = new List<FunctionSchema>();
        await using (var cmd = CreateCommand(sql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                functions.Add(new FunctionSchema
                {
                    SchemaName = GetString(reader, 0),
                    FunctionName = GetString(reader, 1),
                    FunctionType = GetString(reader, 2),
                    Definition = GetNullableString(reader, 3),
                });
            }
        }

        // ── Function parameters ──
        var paramsSql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, o.name, pr.name, tp.name, pr.max_length, pr.is_output
                FROM sys.parameters pr
                INNER JOIN sys.objects o ON pr.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                INNER JOIN sys.types tp ON pr.user_type_id = tp.user_type_id
                WHERE o.type IN ('FN', 'IF', 'TF') AND o.is_ms_shipped = 0 AND pr.name <> ''
                ORDER BY s.name, o.name, pr.parameter_id
                """,
            DatabaseProvider.PostgreSql => """
                SELECT n.nspname, p.proname, pa.parameter_name, pa.data_type,
                       pa.character_maximum_length,
                       CASE WHEN pa.parameter_mode = 'OUT' OR pa.parameter_mode = 'INOUT' THEN true ELSE false END
                FROM information_schema.parameters pa
                JOIN information_schema.routines r
                    ON pa.specific_schema = r.specific_schema AND pa.specific_name = r.specific_name
                JOIN pg_proc p ON r.routine_name = p.proname
                JOIN pg_namespace n ON p.pronamespace = n.oid AND n.nspname = r.routine_schema
                WHERE r.routine_type = 'FUNCTION'
                  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
                  AND pa.parameter_name IS NOT NULL AND pa.parameter_name <> ''
                ORDER BY n.nspname, p.proname, pa.ordinal_position
                """,
            DatabaseProvider.MySql => """
                SELECT specific_schema, specific_name, parameter_name, data_type,
                       character_maximum_length,
                       CASE WHEN parameter_mode = 'OUT' OR parameter_mode = 'INOUT' THEN 1 ELSE 0 END
                FROM information_schema.parameters
                WHERE specific_schema = DATABASE()
                  AND parameter_name IS NOT NULL AND parameter_name <> ''
                  AND routine_type = 'FUNCTION'
                ORDER BY specific_schema, specific_name, ordinal_position
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var paramsByFunc = new Dictionary<string, List<ParameterSchema>>();
        await using (var cmd = CreateCommand(paramsSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var key = $"{GetString(reader, 0)}.{GetString(reader, 1)}";
                if (!paramsByFunc.TryGetValue(key, out var pars))
                {
                    pars = [];
                    paramsByFunc[key] = pars;
                }
                pars.Add(new ParameterSchema
                {
                    ParameterName = GetString(reader, 2),
                    DataType = GetString(reader, 3),
                    MaxLength = GetNullableInt(reader, 4),
                    IsOutput = GetBool(reader, 5),
                });
            }
        }

        foreach (var func in functions)
        {
            var key = $"{func.SchemaName}.{func.FunctionName}";
            if (paramsByFunc.TryGetValue(key, out var pars))
                func.Parameters = pars;
        }

        return functions;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TRIGGERS
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<TriggerSchema>> ReadTriggersAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var sql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, tr.name, t.name, m.definition,
                       CASE WHEN tr.is_disabled = 0 THEN 1 ELSE 0 END
                FROM sys.triggers tr
                INNER JOIN sys.tables t ON tr.parent_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN sys.sql_modules m ON tr.object_id = m.object_id
                WHERE tr.is_ms_shipped = 0
                ORDER BY s.name, t.name, tr.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT trigger_schema, trigger_name, event_object_table,
                       action_statement, 1
                FROM information_schema.triggers
                WHERE trigger_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY trigger_schema, event_object_table, trigger_name
                """,
            DatabaseProvider.MySql => """
                SELECT trigger_schema, trigger_name, event_object_table,
                       action_statement, 1
                FROM information_schema.triggers
                WHERE trigger_schema = DATABASE()
                ORDER BY trigger_schema, event_object_table, trigger_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var triggers = new List<TriggerSchema>();
        await using var cmd = CreateCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            triggers.Add(new TriggerSchema
            {
                SchemaName = GetString(reader, 0),
                TriggerName = GetString(reader, 1),
                TableName = GetString(reader, 2),
                Definition = GetNullableString(reader, 3),
                IsEnabled = GetInt(reader, 4) == 1,
            });
        }

        return triggers;
    }

    // ═══════════════════════════════════════════════════════════════
    //  FOREIGN KEYS
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<ForeignKeySchema>> ReadForeignKeysAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var sql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT fk.name, ps.name, pt.name, pc.name, rs.name, rt.name, rc.name,
                       fk.delete_referential_action_desc, fk.update_referential_action_desc
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
                INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
                INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
                INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
                INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
                INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
                ORDER BY ps.name, pt.name, fk.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT tc.constraint_name,
                       kcu.table_schema, kcu.table_name, kcu.column_name,
                       ccu.table_schema, ccu.table_name, ccu.column_name,
                       rc.delete_rule, rc.update_rule
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage ccu
                    ON tc.constraint_name = ccu.constraint_name AND tc.table_schema = ccu.table_schema
                JOIN information_schema.referential_constraints rc
                    ON tc.constraint_name = rc.constraint_name AND tc.table_schema = rc.constraint_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY kcu.table_schema, kcu.table_name, tc.constraint_name
                """,
            DatabaseProvider.MySql => """
                SELECT rc.constraint_name,
                       kcu.table_schema, kcu.table_name, kcu.column_name,
                       kcu.referenced_table_schema, kcu.referenced_table_name, kcu.referenced_column_name,
                       rc.delete_rule, rc.update_rule
                FROM information_schema.referential_constraints rc
                JOIN information_schema.key_column_usage kcu
                    ON rc.constraint_name = kcu.constraint_name AND rc.constraint_schema = kcu.constraint_schema
                WHERE rc.constraint_schema = DATABASE()
                ORDER BY kcu.table_schema, kcu.table_name, rc.constraint_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var foreignKeys = new List<ForeignKeySchema>();
        await using var cmd = CreateCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            foreignKeys.Add(new ForeignKeySchema
            {
                ConstraintName = GetString(reader, 0),
                ParentSchema = GetString(reader, 1),
                ParentTable = GetString(reader, 2),
                ParentColumn = GetString(reader, 3),
                ReferencedSchema = GetString(reader, 4),
                ReferencedTable = GetString(reader, 5),
                ReferencedColumn = GetString(reader, 6),
                DeleteAction = GetNullableString(reader, 7),
                UpdateAction = GetNullableString(reader, 
                SELECT s.name, o.name, o.type_desc, m.definition
                FROM sys.objects o
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                LEFT JOIN sys.sql_modules m ON o.object_id = m.object_id
                WHERE o.type IN ('FN', 'IF', 'TF') AND o.is_ms_shipped = 0
                ORDER BY s.name, o.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT n.nspname, p.proname,
                       CASE p.prokind WHEN 'f' THEN 'SCALAR' WHEN 'a' THEN 'AGGREGATE' WHEN 'w' THEN 'WINDOW' ELSE 'UNKNOWN' END,
                       pg_get_functiondef(p.oid)
                FROM pg_proc p
                JOIN pg_namespace n ON p.pronamespace = n.oid
                WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
                  AND p.prokind IN ('f', 'a', 'w')
                ORDER BY n.nspname, p.proname
                """,
            DatabaseProvider.MySql => """
                SELECT routine_schema, routine_name, 'FUNCTION', routine_definition
                FROM information_schema.routines
                WHERE routine_type = 'FUNCTION' AND routine_schema = DATABASE()
                ORDER BY routine_schema, routine_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var functions = new List<FunctionSchema>();
        await using (var cmd = CreateCommand(sql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                functions.Add(new FunctionSchema
                {
                    SchemaName = GetString(reader, 0),
                    FunctionName = GetString(reader, 1),
                    FunctionType = GetString(reader, 2),
                    Definition = GetNullableString(reader, 3),
                });
            }
        }

        // ── Function parameters ──
        var paramsSql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, o.name, pr.name, tp.name, pr.max_length, pr.is_output
                FROM sys.parameters pr
                INNER JOIN sys.objects o ON pr.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                INNER JOIN sys.types tp ON pr.user_type_id = tp.user_type_id
                WHERE o.type IN ('FN', 'IF', 'TF') AND o.is_ms_shipped = 0 AND pr.name <> ''
                ORDER BY s.name, o.name, pr.parameter_id
                """,
            DatabaseProvider.PostgreSql => """
                SELECT n.nspname, p.proname, pa.parameter_name, pa.data_type,
                       pa.character_maximum_length,
                       CASE WHEN pa.parameter_mode = 'OUT' OR pa.parameter_mode = 'INOUT' THEN true ELSE false END
                FROM information_schema.parameters pa
                JOIN information_schema.routines r
                    ON pa.specific_schema = r.specific_schema AND pa.specific_name = r.specific_name
                JOIN pg_proc p ON r.routine_name = p.proname
                JOIN pg_namespace n ON p.pronamespace = n.oid AND n.nspname = r.routine_schema
                WHERE r.routine_type = 'FUNCTION'
                  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
                  AND pa.parameter_name IS NOT NULL AND pa.parameter_name <> ''
                ORDER BY n.nspname, p.proname, pa.ordinal_position
                """,
            DatabaseProvider.MySql => """
                SELECT specific_schema, specific_name, parameter_name, data_type,
                       character_maximum_length,
                       CASE WHEN parameter_mode = 'OUT' OR parameter_mode = 'INOUT' THEN 1 ELSE 0 END
                FROM information_schema.parameters
                WHERE specific_schema = DATABASE()
                  AND parameter_name IS NOT NULL AND parameter_name <> ''
                  AND routine_type = 'FUNCTION'
                ORDER BY specific_schema, specific_name, ordinal_position
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var paramsByFunc = new Dictionary<string, List<ParameterSchema>>();
        await using (var cmd = CreateCommand(paramsSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var key = $"{GetString(reader, 0)}.{GetString(reader, 1)}";
                if (!paramsByFunc.TryGetValue(key, out var pars))
                {
                    pars = [];
                    paramsByFunc[key] = pars;
                }
                pars.Add(new ParameterSchema
                {
                    ParameterName = GetString(reader, 2),
                    DataType = GetString(reader, 3),
                    MaxLength = GetNullableInt(reader, 4),
                    IsOutput = GetBool(reader, 5),
                });
            }
        }

        foreach (var func in functions)
        {
            var key = $"{func.SchemaName}.{func.FunctionName}";
            if (paramsByFunc.TryGetValue(key, out var pars))
                func.Parameters = pars;
        }

        return functions;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TRIGGERS
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<TriggerSchema>> ReadTriggersAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var sql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT s.name, tr.name, t.name, m.definition,
                       CASE WHEN tr.is_disabled = 0 THEN 1 ELSE 0 END
                FROM sys.triggers tr
                INNER JOIN sys.tables t ON tr.parent_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN sys.sql_modules m ON tr.object_id = m.object_id
                WHERE tr.is_ms_shipped = 0
                ORDER BY s.name, t.name, tr.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT trigger_schema, trigger_name, event_object_table,
                       action_statement, 1
                FROM information_schema.triggers
                WHERE trigger_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY trigger_schema, event_object_table, trigger_name
                """,
            DatabaseProvider.MySql => """
                SELECT trigger_schema, trigger_name, event_object_table,
                       action_statement, 1
                FROM information_schema.triggers
                WHERE trigger_schema = DATABASE()
                ORDER BY trigger_schema, event_object_table, trigger_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var triggers = new List<TriggerSchema>();
        await using var cmd = CreateCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            triggers.Add(new TriggerSchema
            {
                SchemaName = GetString(reader, 0),
                TriggerName = GetString(reader, 1),
                TableName = GetString(reader, 2),
                Definition = GetNullableString(reader, 3),
                IsEnabled = GetInt(reader, 4) == 1,
            });
        }

        return triggers;
    }

    // ═══════════════════════════════════════════════════════════════
    //  FOREIGN KEYS
    // ═══════════════════════════════════════════════════════════════

    private static async Task<List<ForeignKeySchema>> ReadForeignKeysAsync(
        DbConnection connection, DatabaseProvider provider, CancellationToken ct)
    {
        var sql = provider switch
        {
            DatabaseProvider.SqlServer => """
                SELECT fk.name, ps.name, pt.name, pc.name, rs.name, rt.name, rc.name,
                       fk.delete_referential_action_desc, fk.update_referential_action_desc
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
                INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
                INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
                INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
                INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
                INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
                ORDER BY ps.name, pt.name, fk.name
                """,
            DatabaseProvider.PostgreSql => """
                SELECT tc.constraint_name,
                       kcu.table_schema, kcu.table_name, kcu.column_name,
                       ccu.table_schema, ccu.table_name, ccu.column_name,
                       rc.delete_rule, rc.update_rule
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage ccu
                    ON tc.constraint_name = ccu.constraint_name AND tc.table_schema = ccu.table_schema
                JOIN information_schema.referential_constraints rc
                    ON tc.constraint_name = rc.constraint_name AND tc.table_schema = rc.constraint_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY kcu.table_schema, kcu.table_name, tc.constraint_name
                """,
            DatabaseProvider.MySql => """
                SELECT rc.constraint_name,
                       kcu.table_schema, kcu.table_name, kcu.column_name,
                       kcu.referenced_table_schema, kcu.referenced_table_name, kcu.referenced_column_name,
                       rc.delete_rule, rc.update_rule
                FROM information_schema.referential_constraints rc
                JOIN information_schema.key_column_usage kcu
                    ON rc.constraint_name = kcu.constraint_name AND rc.constraint_schema = kcu.constraint_schema
                WHERE rc.constraint_schema = DATABASE()
                ORDER BY kcu.table_schema, kcu.table_name, rc.constraint_name
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };

        var foreignKeys = new List<ForeignKeySchema>();
        await using var cmd = CreateCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            foreignKeys.Add(new ForeignKeySchema
            {
                ConstraintName = GetString(reader, 0),
                ParentSchema = GetString(reader, 1),
                ParentTable = GetString(reader, 2),
                ParentColumn = GetString(reader, 3),
                ReferencedSchema = GetString(reader, 4),
                ReferencedTable = GetString(reader, 5),
                ReferencedColumn = GetString(reader, 6),
                DeleteAction = GetNullableString(reader, 7),
                UpdateAction = GetNullableString(reader, 8),
            });
        }

        return foreignKeys;
    }
}
