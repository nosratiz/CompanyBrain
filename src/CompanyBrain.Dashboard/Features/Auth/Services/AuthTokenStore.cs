namespace CompanyBrain.Dashboard.Features.Auth.Services;

/// <summary>
/// Scoped in-memory store for the current user's auth token and identity info.
/// In Blazor Server each circuit gets its own DI scope, so this is per-user.
/// </summary>
internal sealed class AuthTokenStore
{
    public string? Token { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public void SetOwnerSession(string token, string displayName, string email, string role)
    {
        Token = token;
        DisplayName = displayName;
        Email = email;
        Role = role;
    }

    public void SetEmployeeSession(string token, string firstName, string lastName, string email)
    {
        Token = token;
        DisplayName = $"{firstName} {lastName}";
        Email = email;
        Role = "Employee";
    }

    public void Clear()
    {
        Token = null;
        DisplayName = null;
        Email = null;
        Role = null;
    }
}
