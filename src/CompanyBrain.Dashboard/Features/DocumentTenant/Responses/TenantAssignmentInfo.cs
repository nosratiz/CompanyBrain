namespace CompanyBrain.Dashboard.Features.DocumentTenant.Responses;

/// <summary>
/// Basic tenant info for an assignment.
/// </summary>
public sealed record TenantAssignmentInfo(
    Guid TenantId,
    string TenantName,
    DateTime AssignedAtUtc);
