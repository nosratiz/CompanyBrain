namespace CompanyBrain.Dashboard.Features.DocumentTenant.Responses;

/// <summary>
/// Response containing assignments grouped by document.
/// </summary>
public sealed record DocumentAssignmentsResponse(
    string FileName,
    IReadOnlyList<TenantAssignmentInfo> Tenants);
