namespace CompanyBrain.Dashboard.Features.DocumentTenant.Requests;

/// <summary>
/// A tenant assignment in a request.
/// </summary>
public sealed record TenantAssignment(
    Guid TenantId,
    string TenantName);
