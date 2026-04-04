using CompanyBrain.UserPortal.Server.Domain.Enums;

namespace CompanyBrain.UserPortal.Server.Domain;

public sealed class License
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string PlanName { get; set; }
    public LicenseTier Tier { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int MaxApiKeys { get; set; }
    public int MaxDocuments { get; set; }
    public long MaxStorageBytes { get; set; }
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }
}