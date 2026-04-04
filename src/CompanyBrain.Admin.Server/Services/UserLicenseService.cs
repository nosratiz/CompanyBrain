using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.Admin.Server.Data;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;
using CompanyBrain.Admin.Server.Services.Interfaces;

namespace CompanyBrain.Admin.Server.Services;

public sealed class UserLicenseService : IUserLicenseService
{
    private readonly UserDbContext _dbContext;

    public UserLicenseService(UserDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<License>> GetUserLicensesAsync(Guid userId)
    {
        return await _dbContext.Licenses
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.PurchasedAt)
            .ToListAsync();
    }

    public async Task<License?> GetActiveLicenseAsync(Guid userId)
    {
        return await _dbContext.Licenses
            .Where(l => l.UserId == userId && l.IsActive)
            .Where(l => l.ExpiresAt == null || l.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(l => l.Tier)
            .FirstOrDefaultAsync();
    }

    public async Task<Result<License>> PurchaseLicenseAsync(Guid userId, LicenseTier tier)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
        {
            return Result.Fail<License>("User not found");
        }

        // Deactivate existing licenses of lower tier
        var existingLicenses = await _dbContext.Licenses
            .Where(l => l.UserId == userId && l.IsActive && l.Tier < tier)
            .ToListAsync();

        foreach (var existing in existingLicenses)
        {
            existing.IsActive = false;
        }

        var (maxKeys, maxDocs, maxStorage) = LicensePlanDefaults.GetLimits(tier);
        var license = new License
        {
            UserId = userId,
            PlanName = LicensePlanDefaults.GetPlanName(tier),
            Tier = tier,
            MaxApiKeys = maxKeys,
            MaxDocuments = maxDocs,
            MaxStorageBytes = maxStorage,
            ExpiresAt = LicensePlanDefaults.GetExpiryDate(tier)
        };

        _dbContext.Licenses.Add(license);
        await _dbContext.SaveChangesAsync();

        return Result.Ok(license);
    }

    public async Task<IReadOnlyList<License>> GetAllLicensesAsync(int page, int pageSize)
    {
        return await _dbContext.Licenses
            .Include(l => l.User)
            .OrderByDescending(l => l.PurchasedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalLicenseCountAsync()
    {
        return await _dbContext.Licenses.CountAsync();
    }

    public async Task<Result> RevokeLicenseAsync(Guid licenseId)
    {
        var license = await _dbContext.Licenses.FindAsync(licenseId);
        if (license is null)
            return Result.Fail("License not found");

        license.IsActive = false;
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result<License>> AssignLicenseAsync(Guid userId, LicenseTier tier)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
            return Result.Fail<License>("User not found");

        return await PurchaseLicenseAsync(userId, tier);
    }
}
