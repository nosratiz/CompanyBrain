using CompanyBrain.Dashboard.Features.Auth.Services;
using Microsoft.AspNetCore.Components;

namespace CompanyBrain.Dashboard.Middleware;

/// <summary>
/// Circuit-scoped service that clears auth state and redirects to /login.
/// Inject into Blazor components and call <see cref="HandleIfUnauthorized"/> in catch blocks.
/// </summary>
internal sealed class UnauthorizedRedirectService(
    AuthTokenStore tokenStore,
    NavigationManager navigation,
    ILogger<UnauthorizedRedirectService> logger)
{
    /// <summary>
    /// If the exception is <see cref="UnauthorizedApiException"/>, clears the token and navigates to /login.
    /// Returns true if redirect was triggered, false otherwise.
    /// </summary>
    public async Task<bool> HandleIfUnauthorized(Exception ex)
    {
        if (ex is not UnauthorizedApiException)
            return false;

        logger.LogWarning("External API returned 401 — clearing session and redirecting to login");
        await tokenStore.ClearAsync();
        navigation.NavigateTo("/login", forceLoad: true);
        return true;
    }
}
