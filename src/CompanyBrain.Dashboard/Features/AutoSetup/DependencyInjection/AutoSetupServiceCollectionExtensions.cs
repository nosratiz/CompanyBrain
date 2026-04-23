using CompanyBrain.Dashboard.Features.AutoSetup.Services;

namespace CompanyBrain.Dashboard.Features.AutoSetup.DependencyInjection;

/// <summary>
/// Registers all Auto-Setup services: Claude handshake, M365 device code auth,
/// Copilot manifest generation, and the maintenance worker.
/// </summary>
public static class AutoSetupServiceCollectionExtensions
{
    public static IServiceCollection AddAutoSetup(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<ClaudeHandshakeService>();
        services.AddSingleton<M365DeviceCodeAuthService>();
        services.AddSingleton<CopilotManifestService>();

        // Background maintenance worker
        services.AddSingleton<SovereignMaintenanceWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<SovereignMaintenanceWorker>());

        return services;
    }
}
