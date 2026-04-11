using System.Text.Json;
using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Dashboard.Scripting;
using CompanyBrain.Dashboard.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace CompanyBrain.Dashboard.Mcp.Tools;

/// <summary>
/// Provides dynamic MCP tool handling for custom tools stored in the database.
/// This handler supplements the static [McpServerTool] attributed tools.
/// </summary>
public sealed class DynamicToolHandler(
    IServiceProvider serviceProvider,
    SettingsService settingsService,
    ILogger<DynamicToolHandler> logger)
{
    /// <summary>
    /// Default tenant ID to use when tenant context is not available.
    /// In production, this would come from the MCP session context.
    /// </summary>
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    
    /// <summary>
    /// Default root path for script execution.
    /// In production, this would be resolved per-tenant.
    /// </summary>
    private static readonly string DefaultRootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CompanyBrain",
        "TenantData");
    
    /// <summary>
    /// Lists all enabled custom tools for the current tenant.
    /// This is called by the MCP server when tools/list is requested.
    /// </summary>
    public async ValueTask<IEnumerable<Tool>> ListDynamicToolsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocumentAssignmentDbContext>();
            
            var tenantId = GetCurrentTenantId();
            
            var tools = await dbContext.CustomTools
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && t.IsEnabled)
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken);
            
            logger.LogDebug(
                "Found {Count} enabled custom tools for tenant {TenantId}",
                tools.Count,
                tenantId);
            
            return tools.Select(ConvertToMcpTool);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list dynamic tools");
            return [];
        }
    }
    
    /// <summary>
    /// Executes a custom tool by name with the provided arguments.
    /// This is called by the MCP server when a custom tool is invoked.
    /// </summary>
    public async ValueTask<CallToolResult?> CallDynamicToolAsync(
        string toolName,
        Dictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocumentAssignmentDbContext>();
            var scriptRunner = scope.ServiceProvider.GetRequiredService<ScriptRunnerService>();
            
            var tenantId = GetCurrentTenantId();
            
            var tool = await dbContext.CustomTools
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.TenantId == tenantId && t.Name == toolName && t.IsEnabled,
                    cancellationToken);
            
            if (tool is null)
            {
                logger.LogWarning(
                    "Custom tool '{Name}' not found or disabled for tenant {TenantId}",
                    toolName,
                    tenantId);
                return null; // Signal that this tool is not a dynamic tool
            }
            
            logger.LogInformation(
                "Executing custom tool '{Name}' for tenant {TenantId}",
                toolName,
                tenantId);
            
            var rootPath = GetTenantRootPath(tenantId);
            var context = new ScriptExecutionContext
            {
                Args = arguments ?? new Dictionary<string, object?>(),
                RootPath = rootPath,
                IsWriteEnabled = tool.IsWriteEnabled,
                TenantId = tenantId,
                ToolName = toolName,
                CancellationToken = cancellationToken
            };
            
            var result = await scriptRunner.ExecuteAsync(tool.CSharpCode, context, cancellationToken);
            
            return await CreateToolResponse(result, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute dynamic tool '{Name}'", toolName);
            return CreateErrorResponse($"Failed to execute tool: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Checks if a tool name corresponds to a dynamic (custom) tool.
    /// </summary>
    public async ValueTask<bool> IsDynamicToolAsync(
        string toolName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DocumentAssignmentDbContext>();
            
            var tenantId = GetCurrentTenantId();
            
            return await dbContext.CustomTools
                .AsNoTracking()
                .AnyAsync(
                    t => t.TenantId == tenantId && t.Name == toolName && t.IsEnabled,
                    cancellationToken);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Converts a CustomTool entity to an MCP tool definition.
    /// </summary>
    private static Tool ConvertToMcpTool(CustomTool tool)
    {
        JsonElement inputSchema;
        
        try
        {
            using var doc = JsonDocument.Parse(tool.JsonSchema);
            inputSchema = doc.RootElement.Clone();
        }
        catch
        {
            // Use a default empty schema if parsing fails
            inputSchema = JsonDocument.Parse("""{"type": "object", "properties": {}}""").RootElement;
        }
        
        return new Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = inputSchema
        };
    }
    
    /// <summary>
    /// Gets the current tenant ID.
    /// TODO: In production, extract this from the MCP session context.
    /// </summary>
    private static Guid GetCurrentTenantId()
    {
        // In a real implementation, this would be extracted from:
        // - MCP session metadata
        // - Authentication context
        // - Request headers
        return DefaultTenantId;
    }
    
    /// <summary>
    /// Gets the root path for a tenant's data.
    /// </summary>
    private static string GetTenantRootPath(Guid tenantId)
    {
        var path = Path.Combine(DefaultRootPath, tenantId.ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    
    /// <summary>
    /// Creates an MCP tool response from a script execution result.
    /// Applies governance filtering (PII masking) if enabled.
    /// </summary>
    private async Task<CallToolResult> CreateToolResponse(ScriptExecutionResult result, CancellationToken cancellationToken)
    {
        var outputText = result.GetOutputString();
        
        // Apply PII masking if enabled
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        if (settings.EnablePiiMasking)
        {
            outputText = Helpers.SecurityHelpers.RedactPii(outputText);
        }
        
        var content = new List<ContentBlock>
        {
            new TextContentBlock { Text = outputText }
        };
        
        return new CallToolResult
        {
            Content = content,
            IsError = !result.Success
        };
    }
    
    /// <summary>
    /// Creates an error response for tool execution failures.
    /// </summary>
    private static CallToolResult CreateErrorResponse(string errorMessage)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = errorMessage }],
            IsError = true
        };
    }
}
