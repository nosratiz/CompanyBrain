using CompanyBrain.Dashboard.Helpers;
using CompanyBrain.Dashboard.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CompanyBrain.Dashboard.Mcp;

/// <summary>
/// MCP governance filter that applies security policies, PII redaction, and system prompting
/// based on application settings.
/// </summary>
public sealed class McpGovernanceFilter(
    SettingsService settingsService,
    ILogger<McpGovernanceFilter> logger)
{
    /// <summary>
    /// Filters tool output by applying PII redaction if enabled.
    /// </summary>
    public async Task<CallToolResult> FilterToolOutputAsync(
        CallToolResult result,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        
        if (!settings.EnablePiiMasking)
        {
            return result;
        }

        var filteredContent = new List<ContentBlock>();
        
        foreach (var content in result.Content)
        {
            if (content is TextContentBlock textContent)
            {
                filteredContent.Add(new TextContentBlock
                {
                    Text = SecurityHelpers.RedactPii(textContent.Text)
                });
            }
            else
            {
                filteredContent.Add(content);
            }
        }

        logger.LogDebug("PII filtering applied to tool output");
        
        return new CallToolResult
        {
            Content = filteredContent,
            IsError = result.IsError
        };
    }

    /// <summary>
    /// Applies system prompt prefix to resource content.
    /// </summary>
    public async Task<ReadResourceResult> ApplySystemPromptAsync(
        ReadResourceResult result,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        var prefix = settings.SystemPromptPrefix;
        
        if (string.IsNullOrWhiteSpace(prefix))
        {
            // Also apply PII masking to resources if enabled
            if (settings.EnablePiiMasking)
            {
                return FilterResourceResult(result);
            }
            return result;
        }

        var modifiedContents = new List<ResourceContents>();
        
        foreach (var content in result.Contents)
        {
            var modifiedContent = content switch
            {
                TextResourceContents text => new TextResourceContents
                {
                    Uri = text.Uri,
                    MimeType = text.MimeType,
                    Text = $"{prefix}\n\n{(settings.EnablePiiMasking ? SecurityHelpers.RedactPii(text.Text) : text.Text)}"
                },
                BlobResourceContents blob => blob, // Can't modify binary content
                _ => content
            };
            
            modifiedContents.Add(modifiedContent);
        }

        logger.LogDebug("System prompt prefix applied to resource content");
        
        return new ReadResourceResult { Contents = modifiedContents };
    }

    /// <summary>
    /// Validates that a path is safe according to current security settings.
    /// </summary>
    public async Task<(bool IsValid, string? ErrorMessage)> ValidatePathAsync(
        string requestedPath,
        string tenantBasePath,
        CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetSettingsAsync(cancellationToken);
        
        // In Relaxed mode, skip most checks
        if (settings.SecurityMode == "Relaxed")
        {
            logger.LogDebug("Path validation skipped (Relaxed security mode)");
            return (true, null);
        }

        // Check for directory traversal
        if (!SecurityHelpers.IsPathSafe(requestedPath, tenantBasePath))
        {
            logger.LogWarning("Path validation failed: directory traversal detected for {Path}", requestedPath);
            return (false, "Access denied: path escapes the allowed directory");
        }

        // Check excluded patterns
        var excludedPatterns = await settingsService.GetExcludedPatternsAsync(cancellationToken);
        if (SecurityHelpers.MatchesExcludedPattern(requestedPath, excludedPatterns))
        {
            logger.LogWarning("Path validation failed: matches excluded pattern for {Path}", requestedPath);
            return (false, "Access denied: path matches an excluded pattern");
        }

        return (true, null);
    }

    /// <summary>
    /// Gets whether current security mode requires strict validation.
    /// </summary>
    public async Task<bool> IsStrictModeAsync(CancellationToken cancellationToken = default)
    {
        var mode = await settingsService.GetSecurityModeAsync(cancellationToken);
        return mode == "Strict";
    }

    private ReadResourceResult FilterResourceResult(ReadResourceResult result)
    {
        var modifiedContents = new List<ResourceContents>();
        
        foreach (var content in result.Contents)
        {
            var modifiedContent = content switch
            {
                TextResourceContents text => new TextResourceContents
                {
                    Uri = text.Uri,
                    MimeType = text.MimeType,
                    Text = SecurityHelpers.RedactPii(text.Text)
                },
                _ => content
            };
            
            modifiedContents.Add(modifiedContent);
        }
        
        return new ReadResourceResult { Contents = modifiedContents };
    }
}
