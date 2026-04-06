using Microsoft.EntityFrameworkCore;
using FluentResults;
using CompanyBrain.Admin.Server.Data;
using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;
using CompanyBrain.Admin.Server.Services.Interfaces;

namespace CompanyBrain.Admin.Server.Services;

public sealed class UserService(UserDbContext dbContext) : IUserService
{
    public async Task<Result<User>> RegisterAsync(string email, string password, string fullName, CancellationToken cancellationToken = default)
    {
        if (await EmailExistsAsync(email, cancellationToken))
        {
            return Result.Fail<User>("Email already registered");
        }

        var user = User.Register(email, password, fullName);

        dbContext.Users.Add(user);
        var license = user.CreateLicense(LicenseTier.Free);

        dbContext.Licenses.Add(license);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok(user);
    }

    public async Task<Result<User>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);

        if (user is null)
        {
            return Result.Fail<User>("Invalid email or password");
        }

        var signInResult = user.SignIn(password);
        if (signInResult.IsFailed)
        {
            return Result.Fail<User>(signInResult.Errors);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok(user);
    }

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .Include(u => u.Licenses)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllUsersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .Include(u => u.Licenses)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalUserCountAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Users.CountAsync(cancellationToken);
    }

    public async Task<Result> UpdateUserAsync(Guid userId, string? fullName, string? email, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
            return Result.Fail("User not found");

        if (email is not null)
        {
            var normalized = email.Trim().ToLowerInvariant();
            if (normalized != user.Email && await EmailExistsAsync(normalized, cancellationToken))
                return Result.Fail("Email already in use");
        }

        user.UpdateProfile(fullName, email);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> SetUserActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
            return Result.Fail("User not found");

        user.SetActiveStatus(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
            return Result.Fail("User not found");

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
