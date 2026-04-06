using System.Text.RegularExpressions;
using FluentResults;

namespace CompanyBrain.MultiTenant.Domain;

/// <summary>
/// Represents a tenant (company/organization) in the multi-tenant system.
/// Each tenant gets isolated MCP resources and API keys.
/// </summary>
public sealed class Tenant
{
    private static readonly Regex InvalidCharsRegex = new("[^a-z0-9\\s-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MultipleDashesRegex = new("-+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    public static Tenant Create(string name, string? description, TenantPlan plan)
    {
        var tenant = new Tenant
        {
            Name = name.Trim(),
            Slug = GenerateSlug(name),
            Description = description?.Trim(),
        };

        tenant.UpdatePlan(plan);
        tenant.UpdatedAt = null;
        return tenant;
    }

    public void UpdatePlan(TenantPlan plan)
    {
        Plan = plan;
        MaxDocuments = GetMaxDocuments(plan);
        MaxApiKeys = GetMaxApiKeys(plan);
        MaxStorageBytes = GetMaxStorageBytes(plan);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        Status = TenantStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result EnsureCanCreateApiKey(int activeApiKeyCount)
    {
        if (Status != TenantStatus.Active)
        {
            return Result.Fail("Cannot create API keys for inactive tenants.");
        }

        if (activeApiKeyCount >= MaxApiKeys)
        {
            return Result.Fail($"API key limit reached ({MaxApiKeys}). Upgrade your plan or revoke unused keys.");
        }

        return Result.Ok();
    }

    public static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = InvalidCharsRegex.Replace(slug, string.Empty);
        slug = WhitespaceRegex.Replace(slug, "-");
        slug = MultipleDashesRegex.Replace(slug, "-");
        return slug.Trim('-');
    }

    private static int GetMaxDocuments(TenantPlan plan) => plan switch
    {
        TenantPlan.Free => 100,
        TenantPlan.Starter => 500,
        TenantPlan.Professional => 2_000,
        TenantPlan.Enterprise => 50_000,
        _ => 100
    };

    private static int GetMaxApiKeys(TenantPlan plan) => plan switch
    {
        TenantPlan.Free => 3,
        TenantPlan.Starter => 10,
        TenantPlan.Professional => 25,
        TenantPlan.Enterprise => 100,
        _ => 3
    };

    private static long GetMaxStorageBytes(TenantPlan plan) => plan switch
    {
        TenantPlan.Free => 100 * 1024 * 1024,
        TenantPlan.Starter => 1024L * 1024 * 1024,
        TenantPlan.Professional => 10L * 1024 * 1024 * 1024,
        TenantPlan.Enterprise => 100L * 1024 * 1024 * 1024,
        _ => 100 * 1024 * 1024
    };
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
