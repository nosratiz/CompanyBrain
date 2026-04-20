namespace CompanyBrain.Dashboard.Features.DeepClean;

/// <summary>
/// Registers DeepClean background service and its dependencies.
/// </summary>
public static class DeepCleanServiceCollectionExtensions
{
    public static IServiceCollection AddDeepClean(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options from "DeepClean" section — falls back to defaults if absent
        services.Configure<DeepCleanOptions>(
            configuration.GetSection(DeepCleanOptions.SectionName));

        // Repository is singleton (uses IDbContextFactory for thread-safe DB access)
        services.AddSingleton<DeepCleanRepository>();

        // Background service
        services.AddSingleton<DeepCleanService>();
        services.AddHostedService(sp => sp.GetRequiredService<DeepCleanService>());

        return services;
    }
}
