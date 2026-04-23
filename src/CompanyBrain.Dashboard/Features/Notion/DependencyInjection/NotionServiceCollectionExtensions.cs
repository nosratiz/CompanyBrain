using CompanyBrain.Dashboard.Features.Notion.Api;
using CompanyBrain.Dashboard.Features.Notion.Services;

namespace CompanyBrain.Dashboard.Features.Notion.DependencyInjection;

/// <summary>
/// Registers Notion integration services.
/// </summary>
public static class NotionServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="NotionSettingsProvider"/> and <see cref="NotionApiClient"/> to the DI container.
    /// </summary>
    public static IServiceCollection AddNotion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<NotionSettingsProvider>();

        services.AddHttpClient<NotionApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.notion.com/");
        });

        return services;
    }
}
