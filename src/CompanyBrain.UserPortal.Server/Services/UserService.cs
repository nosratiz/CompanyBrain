using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.UserPortal.Server.Data;
using CompanyBrain.UserPortal.Server.Domain;

namespace CompanyBrain.UserPortal.Server.Services;

public interface IUserService
{
    Task<Result<User>> RegisterAsync(string email, string password, string fullName);
    Task<Result<User>> LoginAsync(string email, string password);
    Task<User?> GetByIdAsync(Guid userId);
    Task<bool> EmailExistsAsync(string email);
}

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
        return await _dbContext.Users.FindAsync(userId);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbContext.Users.AnyAsync(u => u.Email == email.ToLowerInvariant());
    }
}
