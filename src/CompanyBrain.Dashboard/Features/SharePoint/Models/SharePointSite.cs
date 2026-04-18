namespace CompanyBrain.Dashboard.Features.SharePoint.Models;

/// <summary>
/// Represents a SharePoint site returned from Microsoft Graph.
/// </summary>
public sealed record SharePointSite(
    string Id,
    string DisplayName,
    string WebUrl,
    string? Description,
    DateTimeOffset CreatedDateTime);
