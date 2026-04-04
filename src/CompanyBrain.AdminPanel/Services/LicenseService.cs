using System.Net.Http.Json;
using System.Net.Http.Headers;
using CompanyBrain.AdminPanel.Models;

namespace CompanyBrain.AdminPanel.Services;

public interface ILicenseService
{
    Task<IReadOnlyList<UserLicense>> GetMyLicensesAsync();
    Task<LicenseResponse?> PurchaseLicenseAsync(LicenseTier tier);
}

public sealed class LicenseService : ILicenseService
{
    private readonly HttpClient _httpClient;
    private readonly AuthStateProvider _authState;

    public LicenseService(HttpClient httpClient, AuthStateProvider authState)
    {
        _httpClient = httpClient;
        _authState = authState;
    }

    public async Task<IReadOnlyList<UserLicense>> GetMyLicensesAsync()
    {
        await SetAuthHeaderAsync();
        var result = await _httpClient.GetFromJsonAsync<IReadOnlyList<UserLicense>>("/api/user/licenses");
        return result ?? [];
    }

    public async Task<LicenseResponse?> PurchaseLicenseAsync(LicenseTier tier)
    {
        await SetAuthHeaderAsync();
        var request = new PurchaseLicenseRequest(tier);
        var response = await _httpClient.PostAsJsonAsync("/api/user/licenses", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<LicenseResponse>();
        }
        return null;
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
