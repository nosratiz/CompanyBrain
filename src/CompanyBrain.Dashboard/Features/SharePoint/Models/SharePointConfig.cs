namespace CompanyBrain.Dashboard.Features.SharePoint.Models;

/// <summary>
/// Configuration options for SharePoint sync.
/// </summary>
public sealed class SharePointSyncOptions
{
    public const string SectionName = "SharePointSync";

    /// <summary>
    /// Azure AD Client/Application ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Client Secret (for daemon/service scenarios).
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Local folder base path for mirrored files.
    /// Default: C:\NexusData or ~/NexusData on macOS/Linux.
    /// </summary>
    public string LocalBasePath { get; set; } = OperatingSystem.IsWindows()
        ? @"C:\NexusData"
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "NexusData");

    /// <summary>
    /// Interval in minutes between sync operations.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Chunk size for downloading large files (in bytes).
    /// Default: 4MB.
    /// </summary>
    public int DownloadChunkSize { get; set; } = 4 * 1024 * 1024;

    /// <summary>
    /// Microsoft Graph scopes required for SharePoint access.
    /// </summary>
    public string[] GraphScopes { get; set; } =
    [
        "https://graph.microsoft.com/Sites.Read.All",
        "https://graph.microsoft.com/Files.Read.All",
        "offline_access"
    ];
}
