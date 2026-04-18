namespace CompanyBrain.Dashboard.Data.Models;

/// <summary>
/// Represents a custom MCP tool that can be dynamically created and executed.
/// Stored locally in SQLite for the Dynamic MCP Tool Builder feature.
/// </summary>
public sealed class CustomTool
{
    public int Id { get; set; }
    
    /// <summary>
    /// The tenant that owns this custom tool.
    /// </summary>
    public required Guid TenantId { get; set; }
    
    /// <summary>
    /// The unique name of the tool (must be valid for MCP tool naming).
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// A human-readable description of what the tool does.
    /// This is shown to the AI/LLM to help it understand when to use the tool.
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// JSON Schema defining the input parameters for the tool.
    /// This schema is used by the MCP protocol to validate arguments.
    /// </summary>
    public required string JsonSchema { get; set; }
    
    /// <summary>
    /// The C# script code that executes when the tool is invoked.
    /// This code runs in a sandboxed Roslyn scripting environment.
    /// </summary>
    public required string CSharpCode { get; set; }
    
    /// <summary>
    /// Whether this tool is currently enabled and available for use.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Whether this tool has write access to the file system.
    /// If false, the tool is read-only and cannot modify files.
    /// </summary>
    public bool IsWriteEnabled { get; set; }
    
    /// <summary>
    /// When this tool was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this tool was last updated.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }
    
    /// <summary>
    /// The user who created this tool (optional tracking).
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// Version number for optimistic concurrency.
    /// </summary>
    public int Version { get; set; } = 1;
}
