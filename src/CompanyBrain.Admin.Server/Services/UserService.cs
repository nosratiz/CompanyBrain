using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.Admin.Server.Data;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;
using CompanyBrain.Admin.Server.Services.Interfaces;

namespace CompanyBrain.Admin.Server.Services;

public sealed class UserService : IUserService
{
    private readonly UserDbContext _dbContext;

    public UserService(UserDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<User>> RegisterAsync(string email, string password, string fullName)
    {
        if (await EmailExistsAsync(email))
        {
            return Result.Fail<User>("Email already registered");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new User
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName
        };

        _dbContext.Users.Add(user);

        // Create free license by default
        var (maxKeys, maxDocs, maxStorage) = LicensePlanDefaults.GetLimits(LicenseTier.Free);
        var license = new License
        {
            UserId = user.Id,
            PlanName = LicensePlanDefaults.GetPlanName(LicenseTier.Free),
            Tier = LicenseTier.Free,
            MaxApiKeys = maxKeys,
            MaxDocuments = maxDocs,
            MaxStorageBytes = maxStorage,
            ExpiresAt = LicensePlanDefaults.GetExpiryDate(LicenseTier.Free)
        };

        _dbContext.Licenses.Add(license);
        await _dbContext.SaveChangesAsync();

        return Result.Ok(user);
    }

    public async Task<Result<User>> LoginAsync(string email, string password)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

        if (user is null)
        {
            return Result.Fail<User>("Invalid email or password");
        }

        if (!user.IsActive)
        {
            return Result.Fail<User>("Account is deactivated");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return Result.Fail<User>("Invalid email or password");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Result.Ok(user);
    }

    public async Task<User?> GetByIdAsync(Guid userId)
    {
        return await _dbContext.Users
            .Include(u => u.Licenses)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbContext.Users.AnyAsync(u => u.Email == email.ToLowerInvariant());
    }

    public async Task<IReadOnlyList<User>> GetAllUsersAsync(int page, int pageSize)
    {
        return await _dbContext.Users
            .Include(u => u.Licenses)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalUserCountAsync()
    {
        return await _dbContext.Users.CountAsync();
    }

    public async Task<Result> UpdateUserAsync(Guid userId, string? fullName, string? email)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
            return Result.Fail("User not found");

        if (email is not null)
        {
            var normalized = email.Trim().ToLowerInvariant();
            if (normalized != user.Email && await EmailExistsAsync(normalized))
                return Result.Fail("Email already in use");
            user.Email = normalized;
        }

        if (fullName is not null)
            user.FullName = fullName.Trim();

        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
            return Result.Fail("User not found");

        user.IsActive = isActive;
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteUserAsync(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user is null)
            return Result.Fail("User not found");

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }
}
