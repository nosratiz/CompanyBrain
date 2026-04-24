namespace CompanyBrain.Dashboard.Services.Audit;

public sealed record AuditEntry(
    string? ActorId = null,
    string? ActorEmail = null,
    string? TenantId = null,
    string? ResourceType = null,
    string? ResourceId = null,
    string? ResourceName = null,
    object? Metadata = null,
    bool Success = true,
    string? ErrorMessage = null,
    string? IpAddress = null);
