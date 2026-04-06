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
        var slug = Tenant.GenerateSlug(name);

        if (await dbContext.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken))
        {
            logger.LogWarning("Tenant creation failed: slug '{Slug}' already exists.", slug);
            return Result.Fail<Tenant>($"A tenant with slug '{slug}' already exists.");
        }

        var tenant = Tenant.Create(name, description, plan);

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

        tenant.UpdatePlan(newPlan);

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

        tenant.Suspend();

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Suspended tenant {TenantId}.", tenantId);

        return Result.Ok();
    }
}
