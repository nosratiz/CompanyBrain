namespace CompanyBrain.Dashboard.Scripting;

/// <summary>
/// The global context object injected into custom tool scripts.
/// Provides access to arguments passed by the AI and the tenant's root path.
/// </summary>
public sealed class ScriptExecutionContext
{
    /// <summary>
    /// The arguments provided by the AI when invoking the tool.
    /// Keys correspond to parameter names defined in the tool's JSON schema.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Args { get; init; }
    
    /// <summary>
    /// The root folder path for the current tenant.
    /// Scripts can use this to access tenant-specific files.
    /// </summary>
    public required string RootPath { get; init; }
    
    /// <summary>
    /// Whether this script has permission to write to the file system.
    /// Scripts should check this before attempting any write operations.
    /// </summary>
    public required bool IsWriteEnabled { get; init; }
    
    /// <summary>
    /// The unique identifier of the current tenant.
    /// </summary>
    public required Guid TenantId { get; init; }
    
    /// <summary>
    /// The name of the tool being executed.
    /// </summary>
    public required string ToolName { get; init; }
    
    /// <summary>
    /// Cancellation token for the script execution.
    /// Scripts should periodically check this to support graceful cancellation.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }
    
    /// <summary>
    /// Gets a typed argument value, or the default if not present.
    /// </summary>
    public T? GetArg<T>(string name, T? defaultValue = default)
    {
        if (!Args.TryGetValue(name, out var value) || value is null)
        {
            return defaultValue;
        }
        
        if (value is T typedValue)
        {
            return typedValue;
        }
        
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
    
    /// <summary>
    /// Gets a required string argument, throwing if not present.
    /// </summary>
    public string GetRequiredString(string name)
    {
        if (!Args.TryGetValue(name, out var value) || value is null)
        {
            throw new ArgumentException($"Required argument '{name}' is missing.");
        }
        
        return value.ToString() ?? throw new ArgumentException($"Argument '{name}' cannot be null.");
    }
    
    /// <summary>
    /// Throws if writing is not enabled for this tool.
    /// Call this before any write operations.
    /// </summary>
    public void EnsureWriteEnabled()
    {
        if (!IsWriteEnabled)
        {
            throw new UnauthorizedAccessException(
                $"Tool '{ToolName}' does not have write permission. " +
                "Enable 'Write-Enabled' in the tool configuration to allow file modifications.");
        }
    }
    
    /// <summary>
    /// Resolves a relative path to an absolute path within the tenant's root.
    /// Ensures the path doesn't escape the root directory.
    /// </summary>
    public string ResolvePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        var normalizedRoot = Path.GetFullPath(RootPath);
        
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Path '{relativePath}' resolves outside the tenant root directory.");
        }
        
        return fullPath;
    }
}
