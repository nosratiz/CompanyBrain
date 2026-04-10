namespace CompanyBrain.Dashboard.Features.Auth.Interfaces;

/// <summary>
/// Interface for persistent auth session storage.
/// </summary>
public interface IAuthSessionStorage
{
    /// <summary>
    /// Saves the authentication session to browser storage.
    /// </summary>
    Task SaveSessionAsync(AuthSessionData session);

    /// <summary>
    /// Retrieves the authentication session from browser storage.
    /// </summary>
    Task<AuthSessionData?> GetSessionAsync();

    /// <summary>
    /// Clears the authentication session from browser storage.
    /// </summary>
    Task ClearSessionAsync();
}

/// <summary>
/// Data transfer object for persisted auth session.
/// </summary>
public sealed record AuthSessionData(
    string Token,
    string DisplayName,
    string Email,
    string Role);
