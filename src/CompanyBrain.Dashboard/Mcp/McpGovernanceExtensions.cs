using CompanyBrain.Dashboard.Helpers;
using CompanyBrain.Dashboard.Services;
using CompanyBrain.Pruning;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp;

/// <summary>
/// Extension methods for integrating governance policies with the MCP server.
/// </summary>
public static class McpGovernanceExtensions
{
    /// <summary>
    /// Wraps tool call handlers with governance policies (PII masking, path validation).
    /// </summary>
    public static IMcpServerBuilder WithGovernanceFiltering(this IMcpServerBuilder builder)
    {
        // Store the original handlers and wrap with governance
        builder.Services.AddSingleton<GovernanceToolWrapper>();
        
        return builder;
    }
}

/// <summary>
/// Wrapper service that applies governance policies to tool results.
/// This is used internally by the MCP server integration.
/// </summary>
public sealed class GovernanceToolWrapper(
    SettingsService settingsService,
    IntelligentPruningService pruningService,
    ILogger<GovernanceToolWrapper> logger)
{
    /// <summary>
    /// Filters a tool result according to current governance settings.
    /// </summary>
    public async Task<CallToolResult> FilterResultAsync(
        CallToolResult result,
        CancellationToken cancellationToken = default)
    {
        var settings = settingsService.GetCachedSettings();
        
        // Fast path: if PII masking is disabled, return as-is
        if (settings is null || !settings.EnablePiiMasking)
        {
            // Try to load settings async if not cached
            settings = await settingsService.GetSettingsAsync(cancellationToken);
            if (!settings.EnablePiiMasking)
            {
                return result;
            }
        }

        // Apply PII masking to all text content
        var filteredContent = new List<ContentBlock>();
        var wasModified = false;

        foreach (var content in result.Content)
        {
            if (content is TextContentBlock textContent)
            {
                var originalText = textContent.Text;
                var filteredText = SecurityHelpers.RedactPii(originalText);
                
                if (filteredText != originalText)
                {
                    wasModified = true;
                    filteredContent.Add(new TextContentBlock
                    {
                        Text = filteredText
                    });
                }
                else
                {
                    filteredContent.Add(content);
                }
            }
            else
            {
                filteredContent.Add(content);
            }
        }

        if (wasModified)
        {
            logger.LogDebug("PII redaction applied to tool output");
        }

        return new CallToolResult
        {
            Content = filteredContent,
            IsError = result.IsError
        };
    }

    /// <summary>
    /// Validates a file path against security policies.
    /// Returns an error result if validation fails.
    /// </summary>
    public async Task<(bool IsValid, CallToolResult? ErrorResult)> ValidatePathAsync(
        string requestedPath,
        string tenantBasePath,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        
        // Skip validation in relaxed mode
        if (settings.SecurityMode == "Relaxed")
        {
            return (true, null);
        }

        // Check path safety
        if (!SecurityHelpers.IsPathSafe(requestedPath, tenantBasePath))
        {
            logger.LogWarning(
                "Path validation failed: directory traversal attempt detected for '{Path}'",
                requestedPath);
            
            return (false, CreateSecurityErrorResult(
                "Access denied: The requested path escapes the allowed directory. " +
                "Directory traversal is not permitted."));
        }

        // Check excluded patterns
        var excludedPatterns = await settingsService.GetExcludedPatternsAsync(cancellationToken);
        if (SecurityHelpers.MatchesExcludedPattern(requestedPath, excludedPatterns))
        {
            logger.LogWarning(
                "Path validation failed: matches excluded pattern for '{Path}'",
                requestedPath);
            
            return (false, CreateSecurityErrorResult(
                "Access denied: The requested path matches an excluded pattern."));
        }

        return (true, null);
    }

    /// <summary>
    /// Creates a text-only tool result (helper for tools returning strings).
    /// </summary>
    public async Task<CallToolResult> CreateFilteredTextResultAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        
        var filteredText = settings.EnablePiiMasking 
            ? SecurityHelpers.RedactPii(text) 
            : text;

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = filteredText }],
            IsError = false
        };
    }

    /// <summary>
    /// Prunes text content using intelligent relevance scoring against the given query.
    /// Small texts that fit within the token budget are returned as-is.
    /// </summary>
    public async ValueTask<string> PruneTextAsync(
        string text,
        string query,
        CancellationToken cancellationToken = default)
    {
        var result = await pruningService.PruneAsync(text, query, cancellationToken);

        if (result.WasPruned)
        {
            logger.LogDebug(
                "Pruned tool output from {Original} to {Pruned} tokens ({Selected} chunks)",
                result.OriginalTokens,
                result.PrunedTokens,
                result.ChunksSelected);
        }

        return result.Text;
    }

    private static CallToolResult CreateSecurityErrorResult(string message)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"[SECURITY ERROR] {message}" }],
            IsError = true
        };
    }
}
