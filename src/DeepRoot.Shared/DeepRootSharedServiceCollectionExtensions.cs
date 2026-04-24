using DeepRoot.Shared.Mcp;
using DeepRoot.Shared.Services;
using DeepRoot.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

namespace DeepRoot.Shared;

/// <summary>
/// Service registration entry point for consumers of DeepRoot.Shared
/// (DeepRoot.Photino, future web hosts, integration tests).
/// </summary>
public static class DeepRootSharedServiceCollectionExtensions
{
    public static IServiceCollection AddDeepRootShared(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMudServices();

        services.TryAddSingleton<IStorageService, LocalStorageService>();
        services.TryAddSingleton<SettingsRepository>();

        // The host application is expected to register an
        // IDeepRootMcpServer implementation when MCP hosting is enabled.
        return services;
    }
}
