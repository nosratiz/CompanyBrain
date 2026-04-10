namespace CompanyBrain.Dashboard.Features.DocumentTenant.Requests;

/// <summary>
/// Request to update assignments for a document (replace all tenants).
/// </summary>
public sealed record UpdateDocumentTenantsRequest(
    string FileName,
    IReadOnlyList<TenantAssignment> Tenants);
