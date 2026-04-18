namespace CompanyBrain.Dashboard.Features.SharePoint.Models;

/// <summary>
/// Represents a SharePoint Document Library (Drive).
/// </summary>
public sealed record SharePointDrive(
    string Id,
    string Name,
    string? Description,
    string DriveType,
    long? QuotaTotal,
    long? QuotaUsed,
    string SiteId);
