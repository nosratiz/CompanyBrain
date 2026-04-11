namespace CompanyBrain.Dashboard.Data.Models;

/// <summary>
/// Centralized application settings for governance and AI behavior control.
/// Uses a single-row configuration pattern in SQLite.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Primary key. Should always be a fixed GUID for single-row pattern.
    /// </summary>
    public Guid Id { get; set; } = AppSettingsConstants.SingletonId;

    /// <summary>
    /// When enabled, PII (emails, API keys, IP addresses) will be redacted from tool outputs.
    /// </summary>
    public bool EnablePiiMasking { get; set; }

    /// <summary>
    /// Maximum storage allowed in gigabytes for the knowledge base.
    /// </summary>
    public int MaxStorageGb { get; set; } = 10;

    /// <summary>
    /// Security mode: "Strict", "Moderate", or "Relaxed".
    /// Strict: Full path validation and PII masking.
    /// Moderate: Path validation only.
    /// Relaxed: Minimal security checks.
    /// </summary>
    public string SecurityMode { get; set; } = "Moderate";

    /// <summary>
    /// Semicolon-separated list of glob patterns for files/paths to exclude from processing.
    /// Example: "*.secret;.env;**/credentials/**"
    /// </summary>
    public string ExcludedPatterns { get; set; } = string.Empty;

    /// <summary>
    /// System prompt prefix to prepend to all AI resource content.
    /// Use this to add governance instructions or context to AI interactions.
    /// </summary>
    public string SystemPromptPrefix { get; set; } = string.Empty;

    /// <summary>
    /// The tenant ID for multi-tenant isolation.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Timestamp when settings were last updated.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When enabled, MCP connections require authentication.
    /// </summary>
    public bool McpRequireAuth { get; set; }

    /// <summary>
    /// Semicolon-separated list of allowed IP addresses/CIDR ranges for MCP connections.
    /// Empty means all IPs are allowed (when McpEnableIpWhitelist is false).
    /// Example: "127.0.0.1;192.168.1.0/24;10.0.0.0/8"
    /// </summary>
    public string McpIpWhitelist { get; set; } = string.Empty;

    /// <summary>
    /// When enabled, only IPs in McpIpWhitelist can connect to MCP.
    /// </summary>
    public bool McpEnableIpWhitelist { get; set; }

    /// <summary>
    /// API key required for MCP connections when McpRequireAuth is enabled.
    /// </summary>
    public string McpApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Constants for AppSettings configuration.
/// </summary>
public static class AppSettingsConstants
{
    /// <summary>
    /// The fixed GUID used for the single-row settings pattern.
    /// </summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");
}
