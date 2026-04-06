using System.Net.Http.Json;
using System.Net.Http.Headers;
using CompanyBrain.AdminPanel.Models;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.AdminPanel.Services;

public interface IAdminService
{
    Task<PaginatedResponse<AdminUserDetail>> GetAllUsersAsync(int page = 1, int pageSize = 20);
    Task<AdminUserDetail?> GetUserByIdAsync(Guid userId);
    Task<AdminUserDetail?> CreateUserAsync(CreateUserRequest request);
    Task<bool> UpdateUserAsync(Guid userId, UpdateUserRequest request);
    Task<bool> SetUserStatusAsync(Guid userId, bool isActive);
    Task<bool> DeleteUserAsync(Guid userId);
    Task<PaginatedResponse<AdminLicenseDetail>> GetAllLicensesAsync(int page = 1, int pageSize = 20);
    Task<bool> AssignLicenseAsync(Guid userId, string tier);
    Task<bool> UpdateLicenseAsync(Guid licenseId, string tier);
    Task<bool> RevokeLicenseAsync(Guid licenseId);
    Task<PaginatedResponse<AdminApiKeyDetail>> GetAllApiKeysAsync(int page = 1, int pageSize = 20);
    Task<bool> AdminRevokeApiKeyAsync(Guid keyId);
}

public sealed class AdminService : IAdminService
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateProvider _authState;
    private readonly ILogger<AdminService> _logger;

    public AdminService(HttpClient httpClient, AuthStateProvider authState, ILogger<AdminService> logger)
    {
        _httpClient = httpClient;
        _authState = authState;
        _logger = logger;
    }

    public async Task<PaginatedResponse<AdminUserDetail>> GetAllUsersAsync(int page = 1, int pageSize = 20)
    {
        await SetAuthHeaderAsync();

        try
        {
            var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<AdminUserDetail>>(
                $"/api/admin/users?page={page}&pageSize={pageSize}");
            return result ?? new PaginatedResponse<AdminUserDetail> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin users for page {Page} with size {PageSize}", page, pageSize);
            throw;
        }
    }

    public async Task<AdminUserDetail?> GetUserByIdAsync(Guid userId)
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.GetFromJsonAsync<AdminUserDetail>($"/api/admin/users/{userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin user {UserId}", userId);
            return null;
        }
    }

    public async Task<AdminUserDetail?> CreateUserAsync(CreateUserRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("/api/admin/users", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AdminUserDetail>();
        }

        _logger.LogWarning("Create user failed with status code {StatusCode} for {Email}", (int)response.StatusCode, request.Email);
        return null;
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/users/{userId}", request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Update user failed with status code {StatusCode} for {UserId}", (int)response.StatusCode, userId);
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetUserStatusAsync(Guid userId, bool isActive)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/users/{userId}/status", new SetUserStatusRequest(isActive));
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Set user status failed with status code {StatusCode} for {UserId}; target active: {IsActive}",
                (int)response.StatusCode,
                userId,
                isActive);
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.DeleteAsync($"/api/admin/users/{userId}");
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Delete user failed with status code {StatusCode} for {UserId}", (int)response.StatusCode, userId);
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<PaginatedResponse<AdminLicenseDetail>> GetAllLicensesAsync(int page = 1, int pageSize = 20)
    {
        await SetAuthHeaderAsync();

        try
        {
            var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<AdminLicenseDetail>>(
                $"/api/admin/licenses?page={page}&pageSize={pageSize}");
            return result ?? new PaginatedResponse<AdminLicenseDetail> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin licenses for page {Page} with size {PageSize}", page, pageSize);
            throw;
        }
    }

    public async Task<bool> AssignLicenseAsync(Guid userId, string tier)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("/api/admin/licenses/assign", new AssignLicenseRequest(userId, tier));
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Assign license failed with status code {StatusCode} for {UserId} using tier {Tier}",
                (int)response.StatusCode,
                userId,
                tier);
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateLicenseAsync(Guid licenseId, string tier)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/licenses/{licenseId}", new UpdateLicenseRequest(tier));
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Update license failed with status code {StatusCode} for {LicenseId} using tier {Tier}",
                (int)response.StatusCode,
                licenseId,
                tier);
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeLicenseAsync(Guid licenseId)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/licenses/{licenseId}/revoke", new { });
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Revoke license failed with status code {StatusCode} for {LicenseId}", (int)response.StatusCode, licenseId);
        }
        return response.IsSuccessStatusCode;
    }

    public async Task<PaginatedResponse<AdminApiKeyDetail>> GetAllApiKeysAsync(int page = 1, int pageSize = 20)
    {
        await SetAuthHeaderAsync();

        try
        {
            var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<AdminApiKeyDetail>>(
                $"/api/admin/api-keys?page={page}&pageSize={pageSize}");
            return result ?? new PaginatedResponse<AdminApiKeyDetail> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin API keys for page {Page} with size {PageSize}", page, pageSize);
            throw;
        }
    }

    public async Task<bool> AdminRevokeApiKeyAsync(Guid keyId)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/api-keys/{keyId}/revoke", new { });
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Admin revoke API key failed with status code {StatusCode} for {KeyId}", (int)response.StatusCode, keyId);
        }
        return response.IsSuccessStatusCode;
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
