using CompanyBrain.MultiTenant.Abstractions;
using CompanyBrain.MultiTenant.Data;
using CompanyBrain.MultiTenant.Middleware;
using CompanyBrain.MultiTenant.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyBrain.MultiTenant.DependencyInjection;

public static class MultiTenantServiceCollectionExtensions
{
    /// <summary>
    /// Adds multi-tenant services to the DI container.
    /// </summary>
    public static IServiceCollection AddCompanyBrainMultiTenant(
        this IServiceCollection services,
        string connectionString,
        string baseStoragePath)
    {
        // Database
        services.AddDbContext<TenantDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Tenant context (request-scoped via AsyncLocal)
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<ITenantContextAccessor>());

        // Core services
        services.AddScoped<TenantService>();
        services.AddScoped<ApiKeyService>();
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<IJwtService, JwtService>();

        // Tenant-scoped knowledge store factory
        services.AddSingleton(sp => new TenantKnowledgeStoreFactory(
            baseStoragePath,
            sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()));

        return services;
    }

    /// <summary>
    /// Ensures the tenant database is created and migrated.
    /// </summary>
    public static async Task EnsureTenantDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
