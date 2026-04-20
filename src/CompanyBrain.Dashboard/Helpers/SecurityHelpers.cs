using System.Text.RegularExpressions;

namespace CompanyBrain.Dashboard.Helpers;

/// <summary>
/// Security helpers for path validation, PII redaction, and content filtering.
/// </summary>
public static partial class SecurityHelpers
{
    private static readonly (Func<Regex> Pattern, string Replacement)[] PiiPatterns =
    [
        (EmailRegex,        "[EMAIL_REDACTED]"),
        (ApiKeyRegex,       "[API_KEY_REDACTED]"),
        (IpAddressRegex,    "[IP_REDACTED]"),
        (GitHubTokenRegex,  "[GITHUB_TOKEN_REDACTED]"),
        (SlackTokenRegex,   "[SLACK_TOKEN_REDACTED]"),
        (AwsAccessKeyRegex, "[AWS_KEY_REDACTED]"),
    ];

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

    public static bool IsPathSafe(string requestedPath, string tenantBasePath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath) || string.IsNullOrWhiteSpace(tenantBasePath))
            return false;

        try
        {
            var normalizedBase = Path.GetFullPath(tenantBasePath).TrimEnd(Path.DirectorySeparatorChar);
            var normalizedRequest = Path.GetFullPath(Path.Combine(tenantBasePath, requestedPath))
                .TrimEnd(Path.DirectorySeparatorChar);
            
            return normalizedRequest.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

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

    public static string RedactPii(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var result = content;
        foreach (var (pattern, replacement) in PiiPatterns)
            result = pattern().Replace(result, replacement);

        return result;
    }

    public static bool ContainsPii(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        foreach (var (pattern, _) in PiiPatterns)
        {
            if (pattern().IsMatch(content))
                return true;
        }

        return false;
    }

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

    public static bool MatchesExcludedPattern(string path, string[] excludedPatterns)
    {
        if (string.IsNullOrEmpty(path) || excludedPatterns.Length == 0)
            return false;

        foreach (var pattern in excludedPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

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
