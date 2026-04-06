using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyBrain.Landing.Client.Models;

namespace CompanyBrain.Landing.Client.Services;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly TokenAuthStateProvider _auth;

    public ApiClient(HttpClient http, TokenAuthStateProvider auth)
    {
        _http = http;
        _auth = auth;
    }

    private void SetAuth()
    {
        if (!string.IsNullOrWhiteSpace(_auth.Token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
    }

    // ── Auth ──
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/register", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task<UserResponse?> GetMeAsync()
    {
        SetAuth();
        return await _http.GetFromJsonAsync<UserResponse>("/api/auth/me");
    }

    // ── Profile ──
    public async Task<ProfileResponse?> GetProfileAsync()
    {
        SetAuth();
        return await _http.GetFromJsonAsync<ProfileResponse>("/api/profile");
    }

    public async Task<ProfileResponse?> UpdateProfileAsync(UpdateProfileRequest request)
    {
        SetAuth();
        var response = await _http.PutAsJsonAsync("/api/profile", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileResponse>();
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        SetAuth();
        var response = await _http.PutAsJsonAsync("/api/profile/password", request);
        response.EnsureSuccessStatusCode();
    }

    // ── Tenants ──
    public async Task<TenantResponse?> CreateTenantAsync(CreateTenantRequest request)
    {
        SetAuth();
        var response = await _http.PostAsJsonAsync("/api/tenants", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantResponse>();
    }

    public async Task<TenantListResponse?> ListTenantsAsync()
    {
        SetAuth();
        return await _http.GetFromJsonAsync<TenantListResponse>("/api/tenants");
    }

    public async Task<TenantResponse?> GetTenantAsync(Guid tenantId)
    {
        SetAuth();
        return await _http.GetFromJsonAsync<TenantResponse>($"/api/tenants/{tenantId}");
    }

    public async Task<TenantResponse?> UpdateTenantPlanAsync(Guid tenantId, UpdateTenantPlanRequest request)
    {
        SetAuth();
        var response = await _http.PutAsJsonAsync($"/api/tenants/{tenantId}/plan", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantResponse>();
    }

    public async Task SuspendTenantAsync(Guid tenantId)
    {
        SetAuth();
        var response = await _http.PostAsync($"/api/tenants/{tenantId}/suspend", null);
        response.EnsureSuccessStatusCode();
    }

    // ── API Keys ──
    public async Task<ApiKeyCreatedResponse?> CreateApiKeyAsync(Guid tenantId, CreateApiKeyRequest request)
    {
        SetAuth();
        var response = await _http.PostAsJsonAsync($"/api/tenants/{tenantId}/api-keys", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();
    }

    public async Task<ApiKeyListResponse?> ListApiKeysAsync(Guid tenantId, bool includeRevoked = false)
    {
        SetAuth();
        return await _http.GetFromJsonAsync<ApiKeyListResponse>(
            $"/api/tenants/{tenantId}/api-keys?includeRevoked={includeRevoked}");
    }

    public async Task RevokeApiKeyAsync(Guid tenantId, Guid keyId, string? reason = null)
    {
        SetAuth();
        var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/tenants/{tenantId}/api-keys/{keyId}")
        {
            Content = reason is not null
                ? JsonContent.Create(new RevokeApiKeyRequest { Reason = reason })
                : null
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task<ApiKeyCreatedResponse?> RegenerateApiKeyAsync(Guid tenantId, Guid keyId)
    {
        SetAuth();
        var response = await _http.PostAsync($"/api/tenants/{tenantId}/api-keys/{keyId}/regenerate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();
    }

    // ── Storage ──
    public async Task<TenantStorageStatsResponse?> GetStorageStatsAsync(Guid tenantId)
    {
        SetAuth();
        return await _http.GetFromJsonAsync<TenantStorageStatsResponse>($"/api/tenants/{tenantId}/storage");
    }

    // ── MCP Connection ──
    public async Task<McpConnectionInfoResponse?> GetMcpConnectionInfoAsync(Guid tenantId)
    {
        SetAuth();
        return await _http.GetFromJsonAsync<McpConnectionInfoResponse>($"/api/tenants/{tenantId}/mcp-connection");
    }
}
