using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace CompanyBrain.Dashboard.Features.Auth.Services;

internal sealed class TokenAuthenticationStateProvider(AuthTokenStore store) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!store.IsAuthenticated)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, store.DisplayName ?? string.Empty),
            new(ClaimTypes.Email, store.Email ?? string.Empty),
            new(ClaimTypes.Role, store.Role ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, "CompanyBrain");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void NotifyAuthStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
