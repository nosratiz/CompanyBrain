using System.Net;

namespace CompanyBrain.Dashboard.Middleware;

/// <summary>
/// DelegatingHandler that intercepts 401 Unauthorized responses from external APIs
/// and throws <see cref="UnauthorizedApiException"/> so Blazor components can clear
/// circuit-scoped auth state and redirect to login.
/// </summary>
public sealed class UnauthorizedRedirectHandler(ILogger<UnauthorizedRedirectHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning(
                "Received 401 Unauthorized for {Method} {Uri}. Raising UnauthorizedApiException.",
                request.Method,
                request.RequestUri);

            throw new UnauthorizedApiException(
                $"External API returned 401 for {request.Method} {request.RequestUri}");
        }

        return response;
    }
}

/// <summary>
/// Thrown when an external API responds with 401 Unauthorized.
/// Blazor components should catch this, clear the token store, and redirect to /login.
/// </summary>
public sealed class UnauthorizedApiException(string message) : Exception(message);
