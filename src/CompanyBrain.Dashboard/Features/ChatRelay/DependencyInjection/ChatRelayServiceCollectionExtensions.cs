using CompanyBrain.Dashboard.Features.ChatRelay.Services;

namespace CompanyBrain.Dashboard.Features.ChatRelay.DependencyInjection;

/// <summary>
/// Registers all Sovereign Chat Relay services into the DI container.
/// </summary>
public static class ChatRelayServiceCollectionExtensions
{
    public static IServiceCollection AddChatRelay(this IServiceCollection services)
    {
        // Settings + thread storage
        services.AddSingleton<ChatRelaySettingsService>();
        services.AddSingleton<ConversationThreadRepository>();
        services.AddSingleton<IConversationThreadRepository>(
            sp => sp.GetRequiredService<ConversationThreadRepository>());

        // Post-processor
        services.AddSingleton<SovereignPostProcessor>();

        // Outbound HTTP clients
        services.AddHttpClient("slack", client =>
        {
            client.BaseAddress = new Uri("https://slack.com/");
        });
        services.AddHttpClient("teams"); // Base address set per-call (different service URLs)

        services.AddSingleton<SlackOutboundClient>();
        services.AddSingleton<TeamsOutboundClient>();

        // Core relay service
        services.AddSingleton<ChatRelayService>();

        // Dev tunnel background service
        services.AddSingleton<DevTunnelService>();
        services.AddHostedService(sp => sp.GetRequiredService<DevTunnelService>());

        return services;
    }
}
