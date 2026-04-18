namespace CompanyBrain.Dashboard.Scripting;

/// <summary>
/// Represents the result of executing a custom tool script.
/// </summary>
public sealed class ScriptExecutionResult
{
    /// <summary>
    /// Whether the script executed successfully.
    /// </summary>
    public required bool Success { get; init; }
    
    /// <summary>
    /// The output/return value from the script.
    /// </summary>
    public object? Output { get; init; }
    
    /// <summary>
    /// Error message if the script failed.
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// Detailed exception information (only populated in non-production).
    /// </summary>
    public string? ExceptionDetails { get; init; }
    
    /// <summary>
    /// How long the script took to execute.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }
    
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ScriptExecutionResult Ok(object? output, TimeSpan executionTime) => new()
    {
        Success = true,
        Output = output,
        ExecutionTime = executionTime
    };
    
    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static ScriptExecutionResult Fail(string error, TimeSpan executionTime, Exception? exception = null) => new()
    {
        Success = false,
        Error = error,
        ExceptionDetails = exception?.ToString(),
        ExecutionTime = executionTime
    };
    
    /// <summary>
    /// Creates a timeout result.
    /// </summary>
    public static ScriptExecutionResult Timeout(TimeSpan executionTime) => new()
    {
        Success = false,
        Error = "Script execution timed out (exceeded 5 second limit).",
        ExecutionTime = executionTime
    };
    
    /// <summary>
    /// Creates a security violation result.
    /// </summary>
    public static ScriptExecutionResult SecurityViolation(string message, TimeSpan executionTime) => new()
    {
        Success = false,
        Error = $"Security violation: {message}",
        ExecutionTime = executionTime
    };
    
    /// <summary>
    /// Gets the string representation of the output for MCP responses.
    /// </summary>
    public string GetOutputString()
    {
        if (!Success)
        {
            return Error ?? "Unknown error occurred.";
        }
        
        return Output switch
        {
            null => "Script completed with no output.",
            string s => s,
            _ => System.Text.Json.JsonSerializer.Serialize(Output)
        };
    }
}
