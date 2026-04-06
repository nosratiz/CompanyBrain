using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.Admin.Server.Data;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;
using CompanyBrain.Admin.Server.Services.Interfaces;

namespace CompanyBrain.Admin.Server.Services;

public sealed class UserLicenseService(UserDbContext dbContext) : IUserLicenseService
{
    public async Task<IReadOnlyList<License>> GetUserLicensesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Licenses
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.PurchasedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<License?> GetActiveLicenseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Licenses
            .Where(l => l.UserId == userId && l.IsActive)
            .Where(l => l.ExpiresAt == null || l.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(l => l.Tier)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<License>> PurchaseLicenseAsync(Guid userId, LicenseTier tier, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return Result.Fail<License>("User not found");
        }

        // Deactivate existing licenses of lower tier
        var existingLicenses = await dbContext.Licenses
            .Where(l => l.UserId == userId && l.IsActive && l.Tier < tier)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingLicenses)
        {
            existing.Revoke();
        }

        var license = user.CreateLicense(tier);

        dbContext.Licenses.Add(license);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok(license);
    }

    public async Task<IReadOnlyList<License>> GetAllLicensesAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await dbContext.Licenses
            .Include(l => l.User)
            .OrderByDescending(l => l.PurchasedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalLicenseCountAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Licenses.CountAsync(cancellationToken);
    }

    public async Task<Result> RevokeLicenseAsync(Guid licenseId, CancellationToken cancellationToken = default)
    {
        var license = await dbContext.Licenses.FindAsync([licenseId], cancellationToken);
        if (license is null)
            return Result.Fail("License not found");

        license.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<License>> AssignLicenseAsync(Guid userId, LicenseTier tier, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
            return Result.Fail<License>("User not found");

        return await PurchaseLicenseAsync(userId, tier, cancellationToken);
    }

    public async Task<Result<License>> UpdateLicenseTierAsync(Guid licenseId, LicenseTier newTier, CancellationToken cancellationToken = default)
    {
        var license = await dbContext.Licenses.FindAsync([licenseId], cancellationToken);
        if (license is null)
            return Result.Fail<License>("License not found");

        license.UpdateTier(newTier);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok(license);
    }
}
