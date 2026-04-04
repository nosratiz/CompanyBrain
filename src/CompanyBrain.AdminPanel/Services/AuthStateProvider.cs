using Blazored.LocalStorage;
using CompanyBrain.AdminPanel.Models;

namespace CompanyBrain.AdminPanel.Services;

public sealed class AuthStateProvider
{
    private const string TokenKey = "auth_token";
    private const string UserKey = "auth_user";

    private readonly ILocalStorageService _localStorage;

    public event Action? OnChange;

    public AuthStateProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
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
        await _localStorage.SetItemAsync(UserKey, response.User);
        NotifyStateChanged();
    }

    public async Task ClearAuthAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
