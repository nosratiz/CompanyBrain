using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.AdminPanel.Services;

public sealed class UnauthorizedRedirectHandler(
    NavigationManager navigation,
    AuthStateProvider authState,
    ILogger<UnauthorizedRedirectHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning(
                "Received 401 for {Method} {RequestUri}; clearing auth state and redirecting to login",
                request.Method,
                request.RequestUri?.ToString() ?? "<unknown>");
            await authState.ClearAuthAsync();
            navigation.NavigateTo("/login", forceLoad: true);
        }

        return response;
    }
}
