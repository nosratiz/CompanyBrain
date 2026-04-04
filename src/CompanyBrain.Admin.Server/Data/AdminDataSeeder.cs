using CompanyBrain.Admin.Server.Domain;
using CompanyBrain.Admin.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Admin.Server.Data;

internal static class AdminDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminDataSeeder");

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var options = configuration.GetSection(AdminSeedOptions.SectionName).Get<AdminSeedOptions>() ?? new AdminSeedOptions();
        if (!options.Enabled)
        {
            logger.LogInformation("Admin panel seed data is disabled.");
            return;
        }

        var normalizedEmail = options.Email.Trim().ToLowerInvariant();
        var existingUser = await dbContext.Users
            .Include(user => user.Licenses)
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser is null)
        {
            var user = new User
            {
                Email = normalizedEmail,
                FullName = options.FullName.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.Password)
            };

            dbContext.Users.Add(user);
            dbContext.Licenses.Add(CreateFreeLicense(user.Id));
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Seeded default user '{Email}' for the admin panel.", normalizedEmail);
            return;
        }

        var hasActiveLicense = existingUser.Licenses.Any();
        if (!hasActiveLicense)
        {
            dbContext.Licenses.Add(CreateFreeLicense(existingUser.Id));
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Added missing default license for seeded user '{Email}'.", normalizedEmail);
        }
    }

    private static License CreateFreeLicense(Guid userId)
    {
        var (maxApiKeys, maxDocuments, maxStorageBytes) = LicensePlanDefaults.GetLimits(LicenseTier.Free);

        return new License
        {
            UserId = userId,
            PlanName = LicensePlanDefaults.GetPlanName(LicenseTier.Free),
            Tier = LicenseTier.Free,
            MaxApiKeys = maxApiKeys,
            MaxDocuments = maxDocuments,
            MaxStorageBytes = maxStorageBytes,
            ExpiresAt = LicensePlanDefaults.GetExpiryDate(LicenseTier.Free)
        };
    }
}