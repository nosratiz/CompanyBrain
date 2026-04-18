using System.Net;
using Microsoft.AspNetCore.Components;

namespace CompanyBrain.Dashboard.Middleware;

/// <summary>
/// DelegatingHandler that intercepts 401 Unauthorized responses and navigates to the login page.
/// Used with HttpClient instances in Blazor Server to handle authentication expiration.
/// </summary>
public sealed class UnauthorizedRedirectHandler(IServiceProvider serviceProvider, ILogger<UnauthorizedRedirectHandler> logger) : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<UnauthorizedRedirectHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                "Received 401 Unauthorized response for {Method} {Uri}. Redirecting to login.",
                request.Method,
                request.RequestUri);

            TryNavigateToLogin();
        }

        return response;
    }

    private void TryNavigateToLogin()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var navigation = scope.ServiceProvider.GetService<NavigationManager>();
            navigation?.NavigateTo("/login", forceLoad: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not navigate to login page (this is expected for non-Blazor contexts).");
        }
    }
}
