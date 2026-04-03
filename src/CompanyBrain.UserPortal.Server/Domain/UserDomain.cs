namespace CompanyBrain.UserPortal.Server.Domain;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<License> Licenses { get; set; } = [];
    public ICollection<UserApiKey> ApiKeys { get; set; } = [];
}

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

public enum LicenseTier
{
    Free = 0,
    Starter = 1,
    Professional = 2,
    Enterprise = 3
}

public sealed class UserApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string KeyHash { get; set; }
    public required string KeyPrefix { get; set; }
    public ApiKeyScope Scope { get; set; } = ApiKeyScope.ReadOnly;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public string? RevokedReason { get; set; }

    public User? User { get; set; }
}

public enum ApiKeyScope
{
    ReadOnly = 0,
    WriteDocuments = 1,
    ManageResources = 2,
    Admin = 3
}

public static class LicensePlanDefaults
{
    public static (int MaxApiKeys, int MaxDocuments, long MaxStorageBytes) GetLimits(LicenseTier tier) => tier switch
    {
        LicenseTier.Free => (3, 100, 1L * 1024 * 1024 * 1024),       // 1 GB
        LicenseTier.Starter => (10, 500, 5L * 1024 * 1024 * 1024),   // 5 GB
        LicenseTier.Professional => (25, 2_000, 25L * 1024 * 1024 * 1024), // 25 GB
        LicenseTier.Enterprise => (100, 50_000, 100L * 1024 * 1024 * 1024), // 100 GB
        _ => (3, 100, 1L * 1024 * 1024 * 1024)
    };

    public static string GetPlanName(LicenseTier tier) => tier switch
    {
        LicenseTier.Free => "Free",
        LicenseTier.Starter => "Starter",
        LicenseTier.Professional => "Professional",
        LicenseTier.Enterprise => "Enterprise",
        _ => "Unknown"
    };

    public static DateTime? GetExpiryDate(LicenseTier tier) => tier switch
    {
        LicenseTier.Free => null, // Never expires
        _ => DateTime.UtcNow.AddYears(1) // 1 year for paid plans
    };
}
