namespace CompanyBrain.Dashboard.Features.Auth.Contracts;

public sealed record EmployeeAuthResponse(
    string Token,
    EmployeeAuthInfo Employee);

public sealed record EmployeeAuthInfo(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? JobTitle,
    IReadOnlyList<EmployeeTenantAccessInfo> Tenants);

public sealed record EmployeeTenantAccessInfo(
    Guid TenantId,
    string TenantName,
    string Role);
