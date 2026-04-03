using System.Net.Http.Json;
using System.Net.Http.Headers;
using CompanyBrain.UserPortal.Models;

namespace CompanyBrain.UserPortal.Services;

public interface IApiKeyService
{
    Task<IReadOnlyList<UserApiKey>> GetMyApiKeysAsync();
    Task<ApiKeyCreatedResponse?> CreateApiKeyAsync(CreateApiKeyRequest request);
    Task<bool> RevokeApiKeyAsync(Guid keyId);
}

public sealed class ApiKeyService : IApiKeyService
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateProvider _authState;

    public ApiKeyService(HttpClient httpClient, AuthStateProvider authState)
    {
        _httpClient = httpClient;
        _authState = authState;
    }

    public async Task<IReadOnlyList<UserApiKey>> GetMyApiKeysAsync()
    {
        await SetAuthHeaderAsync();
        var result = await _httpClient.GetFromJsonAsync<IReadOnlyList<UserApiKey>>("/api/user/api-keys");
        return result ?? [];
    }

    public async Task<ApiKeyCreatedResponse?> CreateApiKeyAsync(CreateApiKeyRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("/api/user/api-keys", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();
        }
        return null;
    }

    public async Task<bool> RevokeApiKeyAsync(Guid keyId)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.DeleteAsync($"/api/user/api-keys/{keyId}");
        return response.IsSuccessStatusCode;
    }

    private async Task SetAuthHeaderAsync()
    {
        var token = await _authState.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
