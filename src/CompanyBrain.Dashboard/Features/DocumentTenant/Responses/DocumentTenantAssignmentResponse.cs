namespace CompanyBrain.Dashboard.Features.DocumentTenant.Responses;

/// <summary>
/// Response after assigning a document to a tenant.
/// </summary>
public sealed record DocumentTenantAssignmentResponse(
    int Id,
    string FileName,
    Guid TenantId,
    string TenantName,
    DateTime CreatedAtUtc);
