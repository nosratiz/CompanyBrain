namespace CompanyBrain.MultiTenant.Domain;

/// <summary>
/// Represents a tenant (company/organization) in the multi-tenant system.
/// Each tenant gets isolated MCP resources and API keys.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; init; }
    public string? Description { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public TenantPlan Plan { get; set; } = TenantPlan.Free;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Limits based on plan
    public int MaxDocuments { get; set; } = 100;
    public int MaxApiKeys { get; set; } = 3;
    public long MaxStorageBytes { get; set; } = 100 * 1024 * 1024; // 100MB default

    // Navigation
    public ICollection<ApiKey> ApiKeys { get; init; } = [];
    public ICollection<TenantUser> Users { get; init; } = [];
}

public enum TenantStatus
{
    Pending,
    Active,
    Suspended,
    Deleted
}

public enum TenantPlan
{
    Free,
    Starter,
    Professional,
    Enterprise
}
