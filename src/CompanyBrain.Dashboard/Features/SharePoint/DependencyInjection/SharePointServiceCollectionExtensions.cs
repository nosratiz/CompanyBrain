using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Mcp;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using CompanyBrain.Dashboard.Features.SharePoint.Services;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Features.SharePoint.DependencyInjection;

/// <summary>
/// Extension methods for registering SharePoint Local-Mirror services.
/// </summary>
public static class SharePointServiceCollectionExtensions
{
    /// <summary>
    /// Adds SharePoint Local-Mirror services including OAuth, sync, and MCP integration.
    /// </summary>
    public static IServiceCollection AddSharePointMirror(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Configuration
        services.Configure<SharePointSyncOptions>(
            configuration.GetSection(SharePointSyncOptions.SectionName));

        // Database
        services.AddSharePointDatabase(configuration);

        // Settings provider (reads from DB settings with fallback to appsettings.json)
        services.AddSingleton<SharePointSettingsProvider>();

        // Core services
        services.AddSingleton<SharePointOAuthService>();
        services.AddSingleton<GraphClientFactory>();
        services.AddScoped<SharePointSyncService>();

        // Background sync worker
        services.AddSingleton<SharePointSyncWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<SharePointSyncWorker>());

        // MCP integration
        services.AddSingleton<SharePointResourceHandlers>();

        return services;
    }

    /// <summary>
    /// Adds the SharePoint SQLite database context.
    /// </summary>
    private static IServiceCollection AddSharePointDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SharePointSync")
            ?? "Data Source=sharepoint_sync.db";

        services.AddDbContextFactory<SharePointDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        return services;
    }

    /// <summary>
    /// Initializes the SharePoint database, applying migrations and creating FTS5 index.
    /// </summary>
    public static async Task InitializeSharePointDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SharePointDbContext>>();
        await using var db = await dbContextFactory.CreateDbContextAsync();

        // Ensure database is created
        await db.Database.EnsureCreatedAsync();

        // Create FTS5 virtual table
        await db.EnsureFts5TableAsync();
    }

    /// <summary>
    /// Adds SharePoint tools to the MCP server builder.
    /// </summary>
    public static IMcpServerBuilder WithSharePointTools(this IMcpServerBuilder builder)
    {
        return builder.WithTools<SharePointTools>();
    }
}
