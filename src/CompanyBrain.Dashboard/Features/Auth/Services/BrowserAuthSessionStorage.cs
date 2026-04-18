using CompanyBrain.Dashboard.Features.Auth.Interfaces;
using Microsoft.JSInterop;

namespace CompanyBrain.Dashboard.Features.Auth.Services;

/// <summary>
/// Provides persistent storage for auth sessions using browser localStorage via JS interop.
/// </summary>
internal sealed class BrowserAuthSessionStorage(IJSRuntime jsRuntime) : IAuthSessionStorage
{
    private bool _isPrerendering = true;

    public async Task SaveSessionAsync(AuthSessionData session)
    {
        if (_isPrerendering)
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("authStorage.setSession", session);
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering
        }
    }

    public async Task<AuthSessionData?> GetSessionAsync()
    {
        if (_isPrerendering)
        {
            return null;
        }

        try
        {
            return await jsRuntime.InvokeAsync<AuthSessionData?>("authStorage.getSession");
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering
            return null;
        }
    }

    public async Task ClearSessionAsync()
    {
        if (_isPrerendering)
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("authStorage.clearSession");
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering
        }
    }

    /// <summary>
    /// Call this after the component has rendered to enable JS interop.
    /// </summary>
    public void EnableJsInterop()
    {
        _isPrerendering = false;
    }
}
