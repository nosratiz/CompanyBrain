using CompanyBrain.Dashboard.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace CompanyBrain.Dashboard.Mcp;

/// <summary>
/// Extension methods for adding dynamic MCP tool support.
/// </summary>
public static class DynamicMcpExtensions
{
    /// <summary>
    /// Adds dynamic tool handling to the MCP server.
    /// This enables custom tools stored in the database to be listed and executed.
    /// </summary>
    public static IMcpServerBuilder WithDynamicTools(this IMcpServerBuilder builder)
    {
        // Register the handler
        builder.Services.AddSingleton<DynamicToolHandler>();
        
        // Register a composite tool handler that combines static and dynamic tools
        builder.Services.AddSingleton<CompositeToolHandler>();
        
        return builder;
    }
}

/// <summary>
/// Composite tool handler that manages both static (attribute-based) and dynamic (database) tools.
/// </summary>
public sealed class CompositeToolHandler(
    DynamicToolHandler dynamicHandler,
    ILogger<CompositeToolHandler> logger)
{
    /// <summary>
    /// Gets all dynamic tools to be added to the tool list.
    /// Static tools are handled by the MCP SDK automatically.
    /// </summary>
    public async ValueTask<IEnumerable<Tool>> GetDynamicToolsAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching dynamic tools for MCP tool list");
        return await dynamicHandler.ListDynamicToolsAsync(cancellationToken);
    }
    
    /// <summary>
    /// Attempts to execute a tool as a dynamic tool.
    /// Returns null if the tool is not a dynamic tool (should be handled by static handler).
    /// </summary>
    public async ValueTask<CallToolResult?> TryExecuteDynamicToolAsync(
        string toolName,
        Dictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        // Check if this is a dynamic tool
        var isDynamic = await dynamicHandler.IsDynamicToolAsync(toolName, cancellationToken);
        
        if (!isDynamic)
        {
            logger.LogDebug("Tool '{Name}' is not a dynamic tool", toolName);
            return null;
        }
        
        logger.LogDebug("Executing dynamic tool '{Name}'", toolName);
        return await dynamicHandler.CallDynamicToolAsync(toolName, arguments, cancellationToken);
    }
}

/// <summary>
/// Custom MCP tool type that wraps dynamic tools for the MCP protocol.
/// </summary>
public sealed class DynamicMcpTool
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string JsonSchema { get; init; }
    public required Func<Dictionary<string, object?>?, CancellationToken, ValueTask<CallToolResult>> ExecuteAsync { get; init; }
}
