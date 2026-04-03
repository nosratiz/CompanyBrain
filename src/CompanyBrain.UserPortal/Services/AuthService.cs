using System.Net.Http.Json;
using CompanyBrain.UserPortal.Models;

namespace CompanyBrain.UserPortal.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<RegisterResponse?> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
}

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateProvider _authState;

    public AuthService(HttpClient httpClient, AuthStateProvider authState)
    {
        _httpClient = httpClient;
        _authState = authState;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is not null)
            {
                await _authState.SetAuthAsync(result);
            }
            return result;
        }
        return null;
    }

    public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<RegisterResponse>();
        }
        return null;
    }

    public async Task LogoutAsync()
    {
        await _authState.ClearAuthAsync();
    }
}
