using System.Text.RegularExpressions;

namespace CompanyBrain.Dashboard.Helpers;

/// <summary>
/// Security helpers for path validation, PII redaction, and content filtering.
/// </summary>
public static partial class SecurityHelpers
{
    // Regex patterns for PII detection (compiled for performance)
    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
    
    [GeneratedRegex(@"sk-[a-zA-Z0-9]{20,}", RegexOptions.Compiled)]
    private static partial Regex ApiKeyRegex();
    
    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled)]
    private static partial Regex IpAddressRegex();
    
    [GeneratedRegex(@"(?:ghp|gho|ghu|ghs|ghr)_[a-zA-Z0-9]{36,}", RegexOptions.Compiled)]
    private static partial Regex GitHubTokenRegex();
    
    [GeneratedRegex(@"xox[baprs]-[a-zA-Z0-9-]+", RegexOptions.Compiled)]
    private static partial Regex SlackTokenRegex();
    
    [GeneratedRegex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)]
    private static partial Regex AwsAccessKeyRegex();

    /// <summary>
    /// Validates that a requested path is safe and doesn't escape the tenant's directory.
    /// Prevents directory traversal attacks using ".." sequences.
    /// </summary>
    /// <param name="requestedPath">The path requested by the user/AI.</param>
    /// <param name="tenantBasePath">The base path for the tenant's directory.</param>
    /// <returns>True if the path is safe, false if it attempts to escape the base directory.</returns>
    public static bool IsPathSafe(string requestedPath, string tenantBasePath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
            return false;
        
        if (string.IsNullOrWhiteSpace(tenantBasePath))
            return false;

        try
        {
            // Normalize paths
            var normalizedBase = Path.GetFullPath(tenantBasePath).TrimEnd(Path.DirectorySeparatorChar);
            var normalizedRequest = Path.GetFullPath(Path.Combine(tenantBasePath, requestedPath))
                .TrimEnd(Path.DirectorySeparatorChar);
            
            // Check for directory traversal attempts
            if (requestedPath.Contains(".."))
            {
                // Even after normalization, check if path is still within bounds
                return normalizedRequest.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
            }

            // Ensure the resolved path starts with the base path
            return normalizedRequest.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Any exception (invalid path chars, etc.) means the path is unsafe
            return false;
        }
    }

    /// <summary>
    /// Validates that an absolute path is within the allowed tenant directory.
    /// </summary>
    public static bool IsAbsolutePathSafe(string absolutePath, string tenantBasePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || string.IsNullOrWhiteSpace(tenantBasePath))
            return false;

        try
        {
            var normalizedBase = Path.GetFullPath(tenantBasePath).TrimEnd(Path.DirectorySeparatorChar);
            var normalizedPath = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar);
            
            return normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Redacts PII (Personally Identifiable Information) from content.
    /// Redacts emails, API keys (sk-...), IP addresses, and common tokens.
    /// </summary>
    /// <param name="content">The content to redact.</param>
    /// <returns>Content with PII replaced by [REDACTED] markers.</returns>
    public static string RedactPii(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var result = content;
        
        result = EmailRegex().Replace(result, "[EMAIL_REDACTED]");
        result = ApiKeyRegex().Replace(result, "[API_KEY_REDACTED]");
        result = IpAddressRegex().Replace(result, "[IP_REDACTED]");
        result = GitHubTokenRegex().Replace(result, "[GITHUB_TOKEN_REDACTED]");
        result = SlackTokenRegex().Replace(result, "[SLACK_TOKEN_REDACTED]");
        result = AwsAccessKeyRegex().Replace(result, "[AWS_KEY_REDACTED]");
        
        return result;
    }

    /// <summary>
    /// Checks if content contains any PII that would be redacted.
    /// </summary>
    public static bool ContainsPii(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        return EmailRegex().IsMatch(content)
            || ApiKeyRegex().IsMatch(content)
            || IpAddressRegex().IsMatch(content)
            || GitHubTokenRegex().IsMatch(content)
            || SlackTokenRegex().IsMatch(content)
            || AwsAccessKeyRegex().IsMatch(content);
    }

    /// <summary>
    /// Gets a summary of PII types found in content.
    /// </summary>
    public static PiiDetectionResult DetectPii(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new PiiDetectionResult();

        return new PiiDetectionResult
        {
            EmailCount = EmailRegex().Matches(content).Count,
            ApiKeyCount = ApiKeyRegex().Matches(content).Count,
            IpAddressCount = IpAddressRegex().Matches(content).Count,
            GitHubTokenCount = GitHubTokenRegex().Matches(content).Count,
            SlackTokenCount = SlackTokenRegex().Matches(content).Count,
            AwsKeyCount = AwsAccessKeyRegex().Matches(content).Count
        };
    }

    /// <summary>
    /// Checks if a path matches any of the excluded patterns.
    /// </summary>
    public static bool MatchesExcludedPattern(string path, string[] excludedPatterns)
    {
        if (string.IsNullOrEmpty(path) || excludedPatterns.Length == 0)
            return false;

        foreach (var pattern in excludedPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            // Simple glob matching (supports * and **)
            var regexPattern = GlobToRegex(pattern);
            if (Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private static string GlobToRegex(string glob)
    {
        var regex = Regex.Escape(glob)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/\\\\]*")
            .Replace(@"\?", ".");
        
        return $"^{regex}$|[/\\\\]{regex}$";
    }
}

/// <summary>
/// Result of PII detection analysis.
/// </summary>
public readonly record struct PiiDetectionResult
{
    public int EmailCount { get; init; }
    public int ApiKeyCount { get; init; }
    public int IpAddressCount { get; init; }
    public int GitHubTokenCount { get; init; }
    public int SlackTokenCount { get; init; }
    public int AwsKeyCount { get; init; }
    
    public int TotalCount => EmailCount + ApiKeyCount + IpAddressCount 
        + GitHubTokenCount + SlackTokenCount + AwsKeyCount;
    
    public bool HasPii => TotalCount > 0;
}
