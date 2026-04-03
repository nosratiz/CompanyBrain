using System.Text.RegularExpressions;
using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Domain;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.MultiTenant.Services;

public sealed partial class TenantService(
    TenantDbContext dbContext,
    ILogger<TenantService> logger)
{
    /// <summary>
    /// Creates a new tenant with the given name.
    /// </summary>
    public async Task<Result<Tenant>> CreateTenantAsync(
        string name,
        string? description = null,
        TenantPlan plan = TenantPlan.Free,
        CancellationToken cancellationToken = default)
    {
        var slug = GenerateSlug(name);

        // Check for duplicate slug
        if (await dbContext.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            logger.LogWarning("Tenant creation failed: slug '{Slug}' already exists.", slug);
            return Result.Fail<Tenant>($"A tenant with slug '{slug}' already exists.");
        }

        var tenant = new Tenant
        {
            Name = name,
            Slug = slug,
            Description = description,
            Plan = plan,
            MaxDocuments = GetMaxDocuments(plan),
            MaxApiKeys = GetMaxApiKeys(plan),
            MaxStorageBytes = GetMaxStorageBytes(plan)
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created tenant '{Name}' with ID {TenantId}.", tenant.Name, tenant.Id);
        return Result.Ok(tenant);
    }

    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    public async Task<Result<Tenant>> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .Include(t => t.ApiKeys.Where(k => !k.IsRevoked))
            .Include(t => t.Users.Where(u => u.IsActive))
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Fail<Tenant>($"Tenant {tenantId} not found.");
        }

        return Result.Ok(tenant);
    }

    /// <summary>
    /// Gets a tenant by slug.
    /// </summary>
    public async Task<Result<Tenant>> GetTenantBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .Include(t => t.ApiKeys.Where(k => !k.IsRevoked))
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

        if (tenant is null)
        {
            return Result.Fail<Tenant>($"Tenant with slug '{slug}' not found.");
        }

        return Result.Ok(tenant);
    }

    /// <summary>
    /// Lists all active tenants.
    /// </summary>
    public async Task<IReadOnlyList<Tenant>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Tenants
            .Where(t => t.Status != TenantStatus.Deleted)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Updates tenant plan and adjusts limits.
    /// </summary>
    public async Task<Result<Tenant>> UpdatePlanAsync(
        Guid tenantId,
        TenantPlan newPlan,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.FindAsync([tenantId], cancellationToken);
        if (tenant is null)
        {
            return Result.Fail<Tenant>($"Tenant {tenantId} not found.");
        }

        tenant.Plan = newPlan;
        tenant.MaxDocuments = GetMaxDocuments(newPlan);
        tenant.MaxApiKeys = GetMaxApiKeys(newPlan);
        tenant.MaxStorageBytes = GetMaxStorageBytes(newPlan);
        tenant.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated tenant {TenantId} to plan {Plan}.", tenantId, newPlan);

        return Result.Ok(tenant);
    }

    /// <summary>
    /// Suspends a tenant.
    /// </summary>
    public async Task<Result> SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.FindAsync([tenantId], cancellationToken);
        if (tenant is null)
        {
            return Result.Fail($"Tenant {tenantId} not found.");
        }

        tenant.Status = TenantStatus.Suspended;
        tenant.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Suspended tenant {TenantId}.", tenantId);

        return Result.Ok();
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = SlugInvalidChars().Replace(slug, "");
        slug = SlugWhitespace().Replace(slug, "-");
        slug = SlugMultipleDashes().Replace(slug, "-");
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
        TenantPlan.Free => 100 * 1024 * 1024,           // 100 MB
        TenantPlan.Starter => 1024 * 1024 * 1024,        // 1 GB
        TenantPlan.Professional => 10L * 1024 * 1024 * 1024,  // 10 GB
        TenantPlan.Enterprise => 100L * 1024 * 1024 * 1024,   // 100 GB
        _ => 100 * 1024 * 1024
    };

    [GeneratedRegex("[^a-z0-9\\s-]")]
    private static partial Regex SlugInvalidChars();

    [GeneratedRegex("\\s+")]
    private static partial Regex SlugWhitespace();

    [GeneratedRegex("-+")]
    private static partial Regex SlugMultipleDashes();
}
