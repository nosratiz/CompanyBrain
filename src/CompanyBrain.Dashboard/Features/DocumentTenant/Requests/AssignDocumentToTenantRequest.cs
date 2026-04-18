namespace CompanyBrain.Dashboard.Features.DocumentTenant.Requests;

/// <summary>
/// Request to assign a document to a tenant.
/// </summary>
public sealed record AssignDocumentToTenantRequest(
    string FileName,
    Guid TenantId,
    string TenantName);
