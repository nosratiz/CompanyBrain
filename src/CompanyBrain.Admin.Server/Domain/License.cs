using CompanyBrain.Admin.Server.Domain.Enums;

namespace CompanyBrain.Admin.Server.Domain;

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

    public static License Create(Guid userId, LicenseTier tier)
    {
        var (maxKeys, maxDocs, maxStorage) = LicensePlanDefaults.GetLimits(tier);

        return new License
        {
            UserId = userId,
            PlanName = LicensePlanDefaults.GetPlanName(tier),
            Tier = tier,
            MaxApiKeys = maxKeys,
            MaxDocuments = maxDocs,
            MaxStorageBytes = maxStorage,
            ExpiresAt = LicensePlanDefaults.GetExpiryDate(tier)
        };
    }

    public void Revoke() => IsActive = false;

    public void UpdateTier(LicenseTier newTier)
    {
        var (maxKeys, maxDocs, maxStorage) = LicensePlanDefaults.GetLimits(newTier);

        Tier = newTier;
        PlanName = LicensePlanDefaults.GetPlanName(newTier);
        MaxApiKeys = maxKeys;
        MaxDocuments = maxDocs;
        MaxStorageBytes = maxStorage;
        ExpiresAt = LicensePlanDefaults.GetExpiryDate(newTier);
    }
}