using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace CompanyBrain.Landing.Client.Services;

public sealed class TokenAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private ClaimsPrincipal _current = new(new ClaimsIdentity());

    public string? Token { get; private set; }

    public TokenAuthStateProvider(IJSRuntime js) => _js = js;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", "landing_token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                SetFromToken(token);
            }
        }
        catch
        {
            // SSR or prerender — no JS available
        }

        return new AuthenticationState(_current);
    }

    public async Task LoginAsync(string token)
    {
        SetFromToken(token);
        await _js.InvokeVoidAsync("localStorage.setItem", "landing_token", token);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_current)));
    }

    public async Task LogoutAsync()
    {
        Token = null;
        _current = new ClaimsPrincipal(new ClaimsIdentity());
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "landing_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "landing_user");
        }
        catch { /* ignore during SSR */ }
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_current)));
    }

    private void SetFromToken(string token)
    {
        Token = token;
        var handler = new JwtSecurityTokenHandler();
        if (handler.CanReadToken(token))
        {
            var jwt = handler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            _current = new ClaimsPrincipal(identity);
        }
    }
}
