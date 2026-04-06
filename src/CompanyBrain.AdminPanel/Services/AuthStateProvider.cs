using Blazored.LocalStorage;
using CompanyBrain.AdminPanel.Models;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.AdminPanel.Services;

public sealed class AuthStateProvider
{
    private const string TokenKey = "auth_token";
    private const string UserKey = "auth_user";

    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<AuthStateProvider> _logger;

    public event Action? OnChange;

    public AuthStateProvider(ILocalStorageService localStorage, ILogger<AuthStateProvider> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _localStorage.GetItemAsync<string>(TokenKey);
        return !string.IsNullOrEmpty(token);
    }

    public async Task<UserInfo?> GetUserAsync()
    {
        return await _localStorage.GetItemAsync<UserInfo>(UserKey);
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(TokenKey);
    }

    public async Task SetAuthAsync(LoginResponse response)
    {
        await _localStorage.SetItemAsync(TokenKey, response.Token);
        var user = new UserInfo(response.UserId, response.Email, response.FullName, DateTime.UtcNow);
        await _localStorage.SetItemAsync(UserKey, user);
        _logger.LogInformation("Auth state stored for {Email}", response.Email);
        NotifyStateChanged();
    }

    public async Task UpdateUserAsync(UserInfo user)
    {
        await _localStorage.SetItemAsync(UserKey, user);
        _logger.LogInformation("Stored updated user profile for {Email}", user.Email);
        NotifyStateChanged();
    }

    public async Task ClearAuthAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        _logger.LogInformation("Auth state cleared from local storage");
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
