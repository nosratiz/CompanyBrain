using System.Net.Http.Json;
using System.Net.Http.Headers;
using CompanyBrain.AdminPanel.Models;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.AdminPanel.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<RegisterResponse?> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<UserInfo?> GetCurrentUserAsync();
    Task<UserInfo?> UpdateProfileAsync(UpdateProfileRequest request);
}

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateProvider _authState;
    private readonly ILogger<AuthService> _logger;

    public AuthService(HttpClient httpClient, AuthStateProvider authState, ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _authState = authState;
        _logger = logger;
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
                _logger.LogInformation("User {Email} logged in successfully", result.Email);
            }
            return result;
        }

        _logger.LogWarning("Login failed with status code {StatusCode} for {Email}", (int)response.StatusCode, request.Email);
        return null;
    }

    public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<RegisterResponse>();
        }

        _logger.LogWarning("Registration failed with status code {StatusCode} for {Email}", (int)response.StatusCode, request.Email);
        return null;
    }

    public async Task LogoutAsync()
    {
        await _authState.ClearAuthAsync();
        _logger.LogInformation("User logged out and auth state was cleared");
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.GetFromJsonAsync<UserInfo>("/api/auth/me");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load current user profile");
            return null;
        }
    }

    public async Task<UserInfo?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync("/api/auth/profile", request);
        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<UserInfo>();
            if (user is not null)
            {
                await _authState.UpdateUserAsync(user);
                _logger.LogInformation("Profile updated successfully for {Email}", user.Email);
            }
            return user;
        }

        _logger.LogWarning("Update profile failed with status code {StatusCode}", (int)response.StatusCode);
        return null;
    }

    private async Task SetAuthHeaderAsync()
    {
        var token = await _authState.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _logger.LogDebug("Skipping Authorization header because no auth token is available");
        }
    }
}
