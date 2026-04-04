using System.Net.Http.Json;
using System.Net.Http.Headers;
using CompanyBrain.AdminPanel.Models;

namespace CompanyBrain.AdminPanel.Services;

public interface IAdminService
{
    Task<PaginatedResponse<AdminUserDetail>> GetAllUsersAsync(int page = 1, int pageSize = 20);
    Task<AdminUserDetail?> GetUserByIdAsync(Guid userId);
    Task<bool> UpdateUserAsync(Guid userId, UpdateUserRequest request);
    Task<bool> SetUserStatusAsync(Guid userId, bool isActive);
    Task<bool> DeleteUserAsync(Guid userId);
    Task<PaginatedResponse<AdminLicenseDetail>> GetAllLicensesAsync(int page = 1, int pageSize = 20);
    Task<bool> AssignLicenseAsync(Guid userId, string tier);
    Task<bool> RevokeLicenseAsync(Guid licenseId);
}

public sealed class AdminService : IAdminService
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateProvider _authState;

    public AdminService(HttpClient httpClient, AuthStateProvider authState)
    {
        _httpClient = httpClient;
        _authState = authState;
    }

    public async Task<PaginatedResponse<AdminUserDetail>> GetAllUsersAsync(int page = 1, int pageSize = 20)
    {
        await SetAuthHeaderAsync();
        var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<AdminUserDetail>>(
            $"/api/admin/users?page={page}&pageSize={pageSize}");
        return result ?? new PaginatedResponse<AdminUserDetail> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
    }

    public async Task<AdminUserDetail?> GetUserByIdAsync(Guid userId)
    {
        await SetAuthHeaderAsync();
        try
        {
            return await _httpClient.GetFromJsonAsync<AdminUserDetail>($"/api/admin/users/{userId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/users/{userId}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetUserStatusAsync(Guid userId, bool isActive)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/users/{userId}/status", new SetUserStatusRequest(isActive));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.DeleteAsync($"/api/admin/users/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<PaginatedResponse<AdminLicenseDetail>> GetAllLicensesAsync(int page = 1, int pageSize = 20)
    {
        await SetAuthHeaderAsync();
        var result = await _httpClient.GetFromJsonAsync<PaginatedResponse<AdminLicenseDetail>>(
            $"/api/admin/licenses?page={page}&pageSize={pageSize}");
        return result ?? new PaginatedResponse<AdminLicenseDetail> { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
    }

    public async Task<bool> AssignLicenseAsync(Guid userId, string tier)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("/api/admin/licenses/assign", new AssignLicenseRequest(userId, tier));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeLicenseAsync(Guid licenseId)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/admin/licenses/{licenseId}/revoke", new { });
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
