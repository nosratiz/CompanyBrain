using CompanyBrain.Dashboard.Features.Confluence.Data;
using CompanyBrain.Dashboard.Features.Confluence.Models;
using CompanyBrain.Dashboard.Features.Confluence.Services;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.Confluence.DependencyInjection;

public static class ConfluenceServiceCollectionExtensions
{
    public static IServiceCollection AddConfluenceMirror(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ConfluenceSyncOptions>(
            configuration.GetSection(ConfluenceSyncOptions.SectionName));

        var connectionString = configuration.GetConnectionString("ConfluenceSync")
            ?? "Data Source=confluence_sync.db";

        services.AddDbContextFactory<ConfluenceDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton<ConfluenceSettingsProvider>();

        services.AddHttpClient<ConfluenceApiService>();
        services.AddScoped<ConfluenceSyncService>();

        services.AddSingleton<ConfluenceSyncWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<ConfluenceSyncWorker>());

        return services;
    }

    public static async Task InitializeConfluenceDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConfluenceDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }
}
