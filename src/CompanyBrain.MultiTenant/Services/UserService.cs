using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Domain;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.MultiTenant.Services;

public interface IUserService
{
    Task<Result<TenantUser>> RegisterAsync(string email, string password, string displayName, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TenantUser>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<TenantUser>> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<TenantUser>> UpdateProfileAsync(Guid userId, string? displayName, string? currentPassword, string? newPassword, CancellationToken cancellationToken = default);
}

public sealed class UserService(
    TenantDbContext dbContext,
    ILogger<UserService> logger) : IUserService
{
    public async Task<Result<TenantUser>> RegisterAsync(
        string email,
        string password,
        string displayName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Validate tenant exists
        var tenant = await dbContext.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant is null)
        {
            return Result.Fail<TenantUser>("Tenant not found.");
        }

        // Check if email already exists in this tenant
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, cancellationToken);

        if (existingUser is not null)
        {
            logger.LogWarning("Registration failed: email '{Email}' already exists in tenant {TenantId}.", email, tenantId);
            return Result.Fail<TenantUser>("A user with this email already exists.");
        }

        var user = new TenantUser
        {
            Email = email,
            DisplayName = displayName,
            TenantId = tenantId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = TenantRole.User
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User '{Email}' registered for tenant {TenantId}.", email, tenantId);
        return Result.Ok(user);
    }

    public async Task<Result<TenantUser>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Login failed: user '{Email}' not found.", email);
            return Result.Fail<TenantUser>("Invalid email or password.");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            logger.LogWarning("Login failed: user '{Email}' has no password set.", email);
            return Result.Fail<TenantUser>("Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            logger.LogWarning("Login failed: invalid password for user '{Email}'.", email);
            return Result.Fail<TenantUser>("Invalid email or password.");
        }

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User '{Email}' logged in successfully.", email);
        return Result.Ok(user);
    }

    public async Task<Result<TenantUser>> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user is null)
        {
            return Result.Fail<TenantUser>("User not found.");
        }

        return Result.Ok(user);
    }

    public async Task<Result<TenantUser>> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Result.Fail<TenantUser>("User not found.");
        }

        // Update display name if provided
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
        }

        // Update password if both current and new are provided
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                return Result.Fail<TenantUser>("Current password is required to change password.");
            }

            if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                return Result.Fail<TenantUser>("Current password is incorrect.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            logger.LogInformation("User '{UserId}' changed their password.", userId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User '{UserId}' updated their profile.", userId);

        return Result.Ok(user);
    }
}
