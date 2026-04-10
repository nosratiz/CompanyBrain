namespace CompanyBrain.Models;

public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
    MySql,
}

public sealed class DatabaseSchema
{
    public string DatabaseName { get; set; } = "";
    public string ServerName { get; set; } = "";
    public DatabaseProvider Provider { get; set; }
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public List<TableSchema> Tables { get; set; } = [];
    public List<ViewSchema> Views { get; set; } = [];
    public List<StoredProcedureSchema> StoredProcedures { get; set; } = [];
    public List<FunctionSchema> Functions { get; set; } = [];
    public List<TriggerSchema> Triggers { get; set; } = [];
    public List<ForeignKeySchema> ForeignKeys { get; set; } = [];
}

public sealed class TableSchema
{
    public string SchemaName { get; set; } = "";
    public string TableName { get; set; } = "";
    public List<ColumnSchema> Columns { get; set; } = [];
    public List<IndexSchema> Indexes { get; set; } = [];
}

public sealed class ViewSchema
{
    public string SchemaName { get; set; } = "";
    public string ViewName { get; set; } = "";
    public string? Definition { get; set; }
    public List<ColumnSchema> Columns { get; set; } = [];
}

public sealed class ColumnSchema
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class IndexSchema
{
    public string IndexName { get; set; } = "";
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
    public List<string> Columns { get; set; } = [];
}

public sealed class StoredProcedureSchema
{
    public string SchemaName { get; set; } = "";
    public string ProcedureName { get; set; } = "";
    public string? Definition { get; set; }
    public List<ParameterSchema> Parameters { get; set; } = [];
}

public sealed class FunctionSchema
{
    public string SchemaName { get; set; } = "";
    public string FunctionName { get; set; } = "";
    public string FunctionType { get; set; } = "";
    public string? Definition { get; set; }
    public List<ParameterSchema> Parameters { get; set; } = [];
}

public sealed class ParameterSchema
{
    public string ParameterName { get; set; } = "";
    public string DataType { get; set; } = "";
    public int? MaxLength { get; set; }
    public bool IsOutput { get; set; }
}

public sealed class TriggerSchema
{
    public string SchemaName { get; set; } = "";
    public string TriggerName { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? Definition { get; set; }
    public bool IsEnabled { get; set; }
}

public sealed class ForeignKeySchema
{
    public string ConstraintName { get; set; } = "";
    public string ParentSchema { get; set; } = "";
    public string ParentTable { get; set; } = "";
    public string ParentColumn { get; set; } = "";
    public string ReferencedSchema { get; set; } = "";
    public string ReferencedTable { get; set; } = "";
    public string ReferencedColumn { get; set; } = "";
    public string? DeleteAction { get; set; }
    public string? UpdateAction { get; set; }
}
