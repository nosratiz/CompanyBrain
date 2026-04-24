namespace CompanyBrain.Dashboard.Data.Models;

/// <summary>
/// Persisted record of a single pruning operation.
/// Written by <see cref="CompanyBrain.Dashboard.Mcp.GovernanceToolWrapper"/> so events
/// survive across process restarts and are visible to the Blazor dashboard even when
/// the MCP call originated from a separate stdio process (e.g. Claude Desktop).
/// </summary>
public sealed class PruningEventRecord
{
    public int Id { get; set; }
    public string ToolName { get; set; } = "";
    public string Query { get; set; } = "";
    public string SourceAttribution { get; set; } = "";

    /// <summary>Milliseconds since Unix epoch — stored as INTEGER for SQLite compatibility.</summary>
    public long TimestampUnixMs { get; set; }

    public int OriginalTokens { get; set; }
    public int PrunedTokens { get; set; }
    public int ChunksEvaluated { get; set; }
    public int ChunksSelected { get; set; }
    public bool WasPruned { get; set; }
    public bool PiiDetected { get; set; }

    /// <summary>JSON-serialized <c>List&lt;SnippetDetail&gt;</c>.</summary>
    public string SnippetsJson { get; set; } = "[]";
}
