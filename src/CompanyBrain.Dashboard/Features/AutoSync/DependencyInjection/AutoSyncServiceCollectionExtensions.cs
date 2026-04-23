using CompanyBrain.Dashboard.Features.AutoSync.Providers;
using CompanyBrain.Dashboard.Features.AutoSync.Services;
using CompanyBrain.Dashboard.Features.Confluence.Services;
using CompanyBrain.Dashboard.Features.SharePoint.Services;
using CompanyBrain.Services;

namespace CompanyBrain.Dashboard.Features.AutoSync.DependencyInjection;

/// <summary>
/// Registers all AutoSync services: schedule repository, ingestion providers,
/// provider factory, and the <see cref="SovereignSyncWorker"/> hosted service.
/// </summary>
public static class AutoSyncServiceCollectionExtensions
{
    /// <summary>
    /// Adds the AutoSync background scheduling system to the DI container.
    ///
    /// <para>
    /// Prerequisites (must already be registered before calling this):
    /// <list type="bullet">
    ///   <item><description><see cref="IDbContextFactory{DocumentAssignmentDbContext}"/> — for the schedule repository.</description></item>
    ///   <item><description><see cref="WikiIngester"/> — consumed by <see cref="WebWikiIngestionProvider"/> and <see cref="GitHubWikiIngestionProvider"/>.</description></item>
    ///   <item><description><see cref="KnowledgeStore"/> — consumed by HTML-based providers.</description></item>
    ///   <item><description><see cref="SharePointSyncWorker"/> — consumed by <see cref="SharePointIngestionProvider"/>.</description></item>
    ///   <item><description><see cref="ConfluenceSyncWorker"/> — consumed by <see cref="ConfluenceIngestionProvider"/>.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddAutoSync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Schedule data-access
        services.AddSingleton<ScheduleRepository>();
        services.AddSingleton<IScheduleRepository>(sp => sp.GetRequiredService<ScheduleRepository>());

        // Ingestion providers — each is registered twice:
        //   1. As the concrete type (for tests / direct injection)
        //   2. As IIngestionProvider (for the factory's IEnumerable<IIngestionProvider> ctor)
        services.AddSingleton<WebWikiIngestionProvider>();
        services.AddSingleton<IIngestionProvider>(sp => sp.GetRequiredService<WebWikiIngestionProvider>());

        services.AddSingleton<GitHubWikiIngestionProvider>();
        services.AddSingleton<IIngestionProvider>(sp => sp.GetRequiredService<GitHubWikiIngestionProvider>());

        services.AddSingleton<SharePointIngestionProvider>();
        services.AddSingleton<IIngestionProvider>(sp => sp.GetRequiredService<SharePointIngestionProvider>());

        services.AddSingleton<ConfluenceIngestionProvider>();
        services.AddSingleton<IIngestionProvider>(sp => sp.GetRequiredService<ConfluenceIngestionProvider>());

        services.AddSingleton<NotionIngestionProvider>();
        services.AddSingleton<IIngestionProvider>(sp => sp.GetRequiredService<NotionIngestionProvider>());

        // Factory that indexes providers by SourceType
        services.AddSingleton<IngestionProviderFactory>();

        // Background worker (singleton so it can be injected for manual triggering)
        services.AddSingleton<SovereignSyncWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<SovereignSyncWorker>());

        return services;
    }
}
