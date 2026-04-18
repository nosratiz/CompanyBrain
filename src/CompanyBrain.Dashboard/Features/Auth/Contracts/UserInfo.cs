namespace CompanyBrain.Dashboard.Features.Auth.Contracts;

public sealed record UserInfo(
    Guid Id,
    string Email,
    string DisplayName,
    Guid TenantId,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
