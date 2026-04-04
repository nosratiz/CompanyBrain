using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.UserPortal.Server.Data;
using CompanyBrain.UserPortal.Server.Domain;
using CompanyBrain.UserPortal.Server.Domain.Enums;
using CompanyBrain.UserPortal.Server.Services.Interfaces;

namespace CompanyBrain.UserPortal.Server.Services;

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
}
