using System.Net.Http.Json;

namespace CompanyBrain.Dashboard.Services;

public sealed class TenantApiClient(HttpClient httpClient)
{
    // === Tenant Operations ===

    public Task<TenantListResponse?> ListTenantsAsync() =>
        httpClient.GetFromJsonAsync<TenantListResponse>("/api/tenants");

    public Task<TenantResponse?> GetTenantAsync(Guid tenantId) =>
        httpClient.GetFromJsonAsync<TenantResponse>($"/api/tenants/{tenantId}");

    public async Task<TenantResponse?> CreateTenantAsync(CreateTenantRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("/api/tenants", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantResponse>();
    }

    public async Task<TenantResponse?> UpdateTenantPlanAsync(Guid tenantId, string plan)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/tenants/{tenantId}/plan", new { Plan = plan });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantResponse>();
    }

    public async Task SuspendTenantAsync(Guid tenantId, string? reason = null)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/tenants/{tenantId}/suspend", new { Reason = reason });
        response.EnsureSuccessStatusCode();
    }

    // === API Key Operations ===

    public Task<ApiKeyListResponse?> ListApiKeysAsync(Guid tenantId, bool includeRevoked = false) =>
        httpClient.GetFromJsonAsync<ApiKeyListResponse>($"/api/tenants/{tenantId}/api-keys?includeRevoked={includeRevoked}");

    public async Task<ApiKeyCreatedResponse?> CreateApiKeyAsync(Guid tenantId, CreateApiKeyRequest request)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/tenants/{tenantId}/api-keys", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();
    }

    public async Task RevokeApiKeyAsync(Guid tenantId, Guid keyId, string? reason = null)
    {
        var response = await httpClient.DeleteAsync($"/api/tenants/{tenantId}/api-keys/{keyId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ApiKeyCreatedResponse?> RegenerateApiKeyAsync(Guid tenantId, Guid keyId)
    {
        var response = await httpClient.PostAsync($"/api/tenants/{tenantId}/api-keys/{keyId}/regenerate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>();
    }

    // === Storage Stats ===

    public Task<TenantStorageStatsResponse?> GetStorageStatsAsync(Guid tenantId) =>
        httpClient.GetFromJsonAsync<TenantStorageStatsResponse>($"/api/tenants/{tenantId}/storage");

    // === MCP Connection ===

    public Task<McpConnectionInfoResponse?> GetMcpConnectionInfoAsync(Guid tenantId) =>
        httpClient.GetFromJsonAsync<McpConnectionInfoResponse>($"/api/tenants/{tenantId}/mcp-connection");
}

// === DTO Records ===

public sealed record TenantListResponse(IReadOnlyList<TenantResponse> Tenants);

public sealed record TenantResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string Status,
    string Plan,
    int MaxDocuments,
    int MaxApiKeys,
    long MaxStorageBytes,
    DateTime CreatedAt,
    int ActiveApiKeys,
    int ActiveUsers,
    int DocumentCount = 0);

public sealed record CreateTenantRequest(
    string Name,
    string Slug,
    string Plan = "Free",
    string? Description = null);

public sealed record ApiKeyListResponse(IReadOnlyList<ApiKeyResponse> ApiKeys);

public sealed record ApiKeyResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Scope,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked,
    int RequestsPerMinute,
    int RequestsPerDay);

public sealed record CreateApiKeyRequest(
    string Name,
    string Scope = "ReadOnly",
    DateTime? ExpiresAt = null);

public sealed record ApiKeyCreatedResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    string PlainKey,
    string Scope,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string Warning);

public sealed record TenantStorageStatsResponse(
    int DocumentCount,
    long TotalBytes,
    string FormattedSize,
    long MaxBytes,
    double UsagePercentage);

public sealed record McpConnectionInfoResponse(
    string ServerUrl,
    string Protocol,
    string Transport,
    IReadOnlyList<McpToolInfo> AvailableTools,
    IReadOnlyList<McpResourceInfo> AvailableResources,
    IReadOnlyList<string> SupportedClients,
    string Instructions);

public sealed record McpToolInfo(string Name, string Description);

public sealed record McpResourceInfo(string Uri, string MimeType);
