using MudBlazor;

namespace CompanyBrain.Dashboard.Helpers;

/// <summary>
/// Static helper class for UI display utilities (colors, labels, etc.).
/// </summary>
public static class UiDisplayHelper
{
    /// <summary>
    /// Gets the MudBlazor color for a MIME type.
    /// </summary>
    public static Color GetMimeTypeColor(string? mimeType) => mimeType switch
    {
        "text/markdown" => Color.Primary,
        "text/plain" => Color.Default,
        "text/html" => Color.Info,
        "application/json" => Color.Secondary,
        "application/pdf" => Color.Error,
        _ => Color.Default
    };

    /// <summary>
    /// Gets a human-readable label for a MIME type.
    /// </summary>
    public static string GetMimeTypeLabel(string? mimeType) => mimeType switch
    {
        "text/markdown" => "Markdown",
        "text/plain" => "Text",
        "text/html" => "HTML",
        "application/json" => "JSON",
        "application/pdf" => "PDF",
        _ => mimeType ?? "Unknown"
    };

    /// <summary>
    /// Gets the MudBlazor color for a tenant status.
    /// </summary>
    /// <param name="status">Tenant status: 0=Pending, 1=Active, 2=Suspended, 3=Deleted</param>
    public static Color GetTenantStatusColor(int status) => status switch
    {
        0 => Color.Default,   // Pending
        1 => Color.Success,   // Active
        2 => Color.Warning,   // Suspended
        3 => Color.Error,     // Deleted
        _ => Color.Default
    };

    /// <summary>
    /// Gets the display name for a tenant status.
    /// </summary>
    /// <param name="status">Tenant status: 0=Pending, 1=Active, 2=Suspended, 3=Deleted</param>
    public static string GetTenantStatusName(int status) => status switch
    {
        0 => "Pending",
        1 => "Active",
        2 => "Suspended",
        3 => "Deleted",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the display name for a tenant plan.
    /// </summary>
    /// <param name="plan">Tenant plan: 0=Free, 1=Basic, 2=Professional, 3=Enterprise</param>
    public static string GetTenantPlanName(int plan) => plan switch
    {
        0 => "Free",
        1 => "Basic",
        2 => "Professional",
        3 => "Enterprise",
        _ => "Unknown"
    };
}
