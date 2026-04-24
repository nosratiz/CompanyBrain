namespace CompanyBrain.Dashboard.Data.Audit;

public sealed class AuditLog
{
    public long Id { get; set; }
    public AuditEventType EventType { get; set; }
    public string? ActorId { get; set; }
    public string? ActorEmail { get; set; }
    public string? TenantId { get; set; }
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? Metadata { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}
