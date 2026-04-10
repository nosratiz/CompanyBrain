using CompanyBrain.Dashboard.Features.Auth.Interfaces;

namespace CompanyBrain.Dashboard.Features.Auth.Services;

/// <summary>
/// Scoped in-memory store for the current user's auth token and identity info.
/// In Blazor Server each circuit gets its own DI scope, so this is per-user.
/// Supports persistence to browser localStorage via IAuthSessionStorage.
/// </summary>
internal sealed class AuthTokenStore(IAuthSessionStorage sessionStorage)
{
    public string? Token { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public async Task SetOwnerSessionAsync(string token, string displayName, string email, string role)
    {
        Token = token;
        DisplayName = displayName;
        Email = email;
        Role = role;

        await sessionStorage.SaveSessionAsync(new AuthSessionData(token, displayName, email, role));
    }

    public async Task SetEmployeeSessionAsync(string token, string firstName, string lastName, string email)
    {
        var displayName = $"{firstName} {lastName}";
        const string role = "Employee";

        Token = token;
        DisplayName = displayName;
        Email = email;
        Role = role;

        await sessionStorage.SaveSessionAsync(new AuthSessionData(token, displayName, email, role));
    }

    public async Task ClearAsync()
    {
        Token = null;
        DisplayName = null;
        Email = null;
        Role = null;

        await sessionStorage.ClearSessionAsync();
    }

    /// <summary>
    /// Restores session from persistent storage. Call after JS interop is available.
    /// </summary>
    public async Task<bool> RestoreSessionAsync()
    {
        var session = await sessionStorage.GetSessionAsync();
        if (session is null)
        {
            return false;
        }

        Token = session.Token;
        DisplayName = session.DisplayName;
        Email = session.Email;
        Role = session.Role;

        return true;
    }

    // Legacy synchronous methods for backward compatibility (will not persist)
    public void SetOwnerSession(string token, string displayName, string email, string role)
    {
        Token = token;
        DisplayName = displayName;
        Email = email;
        Role = role;
        _ = sessionStorage.SaveSessionAsync(new AuthSessionData(token, displayName, email, role));
    }

    public void SetEmployeeSession(string token, string firstName, string lastName, string email)
    {
        Token = token;
        DisplayName = $"{firstName} {lastName}";
        Email = email;
        Role = "Employee";
        _ = sessionStorage.SaveSessionAsync(new AuthSessionData(Token, DisplayName, Email, Role));
    }

    public void Clear()
    {
        Token = null;
        DisplayName = null;
        Email = null;
        Role = null;
        _ = sessionStorage.ClearSessionAsync();
    }
}
