namespace CompanyBrain.Dashboard.Features.DocumentTenant.Responses;

/// <summary>
/// Response containing a list of document-tenant assignments.
/// </summary>
public sealed record DocumentTenantAssignmentListResponse(
    IReadOnlyList<DocumentTenantAssignmentResponse> Assignments);
