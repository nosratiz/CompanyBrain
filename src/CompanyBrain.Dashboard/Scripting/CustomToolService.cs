using System.Text.Json;
using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Dashboard.Scripting;

/// <summary>
/// Service for managing custom MCP tools in the database.
/// </summary>
public sealed class CustomToolService(
    DocumentAssignmentDbContext dbContext,
    ScriptRunnerService scriptRunner,
    ILogger<CustomToolService> logger)
{
    /// <summary>
    /// Gets all enabled tools for a tenant.
    /// </summary>
    public async Task<IReadOnlyList<CustomTool>> GetEnabledToolsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomTools
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsEnabled)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
    
    /// <summary>
    /// Gets all tools for a tenant (including disabled).
    /// </summary>
    public async Task<IReadOnlyList<CustomTool>> GetAllToolsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomTools
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }
    
    /// <summary>
    /// Gets a tool by its name for a specific tenant.
    /// </summary>
    public async Task<CustomTool?> GetToolByNameAsync(
        Guid tenantId,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomTools
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId && t.Name == name,
                cancellationToken);
    }
    
    /// <summary>
    /// Gets a tool by its ID.
    /// </summary>
    public async Task<CustomTool?> GetToolByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomTools
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }
    
    /// <summary>
    /// Creates a new custom tool.
    /// </summary>
    public async Task<CustomTool> CreateToolAsync(
        CustomTool tool,
        CancellationToken cancellationToken = default)
    {
        tool.CreatedAtUtc = DateTime.UtcNow;
        tool.Version = 1;
        
        dbContext.CustomTools.Add(tool);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "Created custom tool '{Name}' (ID: {Id}) for tenant {TenantId}",
            tool.Name,
            tool.Id,
            tool.TenantId);
        
        return tool;
    }
    
    /// <summary>
    /// Updates an existing custom tool.
    /// </summary>
    public async Task<CustomTool?> UpdateToolAsync(
        int id,
        Action<CustomTool> updateAction,
        CancellationToken cancellationToken = default)
    {
        var tool = await dbContext.CustomTools
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        
        if (tool is null)
        {
            return null;
        }
        
        updateAction(tool);
        tool.UpdatedAtUtc = DateTime.UtcNow;
        tool.Version++;
        
        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "Updated custom tool '{Name}' (ID: {Id}) to version {Version}",
            tool.Name,
            tool.Id,
            tool.Version);
        
        return tool;
    }
    
    /// <summary>
    /// Deletes a custom tool.
    /// </summary>
    public async Task<bool> DeleteToolAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var tool = await dbContext.CustomTools
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        
        if (tool is null)
        {
            return false;
        }
        
        dbContext.CustomTools.Remove(tool);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation(
            "Deleted custom tool '{Name}' (ID: {Id}) for tenant {TenantId}",
            tool.Name,
            tool.Id,
            tool.TenantId);
        
        return true;
    }
    
    /// <summary>
    /// Toggles the enabled state of a tool.
    /// </summary>
    public async Task<CustomTool?> ToggleToolEnabledAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await UpdateToolAsync(id, tool => tool.IsEnabled = !tool.IsEnabled, cancellationToken);
    }
    
    /// <summary>
    /// Executes a custom tool with the provided arguments.
    /// </summary>
    public async Task<ScriptExecutionResult> ExecuteToolAsync(
        CustomTool tool,
        Dictionary<string, object?> args,
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var context = new ScriptExecutionContext
        {
            Args = args,
            RootPath = rootPath,
            IsWriteEnabled = tool.IsWriteEnabled,
            TenantId = tool.TenantId,
            ToolName = tool.Name,
            CancellationToken = cancellationToken
        };
        
        logger.LogDebug(
            "Executing tool '{Name}' for tenant {TenantId} with args: {Args}",
            tool.Name,
            tool.TenantId,
            JsonSerializer.Serialize(args));
        
        return await scriptRunner.ExecuteAsync(tool.CSharpCode, context, cancellationToken);
    }
    
    /// <summary>
    /// Tests a tool script with sample arguments without saving.
    /// </summary>
    public async Task<ScriptExecutionResult> TestToolAsync(
        string csharpCode,
        bool isWriteEnabled,
        Dictionary<string, object?> testArgs,
        Guid tenantId,
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var context = new ScriptExecutionContext
        {
            Args = testArgs,
            RootPath = rootPath,
            IsWriteEnabled = isWriteEnabled,
            TenantId = tenantId,
            ToolName = "[Test]",
            CancellationToken = cancellationToken
        };
        
        logger.LogDebug(
            "Testing tool script for tenant {TenantId} with args: {Args}",
            tenantId,
            JsonSerializer.Serialize(testArgs));
        
        return await scriptRunner.ExecuteAsync(csharpCode, context, cancellationToken);
    }
    
    /// <summary>
    /// Validates a JSON schema string.
    /// </summary>
    public bool ValidateJsonSchema(string jsonSchema, out string? error)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonSchema);
            var root = doc.RootElement;
            
            // Basic schema validation
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Schema must be a JSON object.";
                return false;
            }
            
            // Check for required "type" property
            if (!root.TryGetProperty("type", out var typeProperty) || 
                typeProperty.GetString() != "object")
            {
                error = "Schema must have \"type\": \"object\".";
                return false;
            }
            
            // Check for "properties" object
            if (!root.TryGetProperty("properties", out var props) ||
                props.ValueKind != JsonValueKind.Object)
            {
                error = "Schema must have a \"properties\" object.";
                return false;
            }
            
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }
}
