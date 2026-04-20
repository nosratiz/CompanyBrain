using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyBrain.Dashboard.Features.Auth.Services;
using CompanyBrain.Dashboard.Middleware;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Client for fetching tenant information from the external tenant management API.
/// </summary>
internal sealed class ExternalTenantApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenStore _tokenStore;
    private readonly ILogger<ExternalTenantApiClient> _logger;

    public ExternalTenantApiClient(
        HttpClient httpClient,
        AuthTokenStore tokenStore,
        ILogger<ExternalTenantApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <summary>
    /// Fetches all tenants from the tenant API.
    /// </summary>
    public async Task<IReadOnlyList<TenantSummaryDto>> GetTenantsAsync(CancellationToken ct = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetFromJsonAsync<TenantListResponseDto>("/api/tenants", ct);
            _logger.LogInformation("Fetched {TenantCount} tenants from external API", response?.Tenants.Count ?? 0);
            return response?.Tenants ?? [];
        }
        catch (UnauthorizedApiException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch tenants from external API");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching tenants");
            return [];
        }
    }

    /// <summary>
    /// Fetches a specific tenant by ID.
    /// </summary>
    public async Task<TenantSummaryDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetFromJsonAsync<TenantSummaryDto>($"/api/tenants/{tenantId}", ct);
            return response;
        }
        catch (UnauthorizedApiException) { throw; }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Tenant {TenantId} not found", tenantId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tenant {TenantId}", tenantId);
            return null;
        }
    }

    private void SetAuthorizationHeader()
    {
        if (!string.IsNullOrEmpty(_tokenStore.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenStore.Token);
        }
    }
}

// Response DTOs matching the external CompanyBrainUserPanel API
public sealed record TenantListResponseDto(IReadOnlyList<TenantSummaryDto> Tenants);

public sealed record TenantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    int Status,
    int Plan,
    DateTime CreatedAt)
{
    /// <summary>
    /// Gets the status as a display string.
    /// </summary>
    public string StatusName => Status switch
    {
        0 => "Pending",
        1 => "Active",
        2 => "Suspended",
        3 => "Deleted",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the plan as a display string.
    /// </summary>
    public string PlanName => Plan switch
    {
        0 => "Free",
        1 => "Basic",
        2 => "Professional",
        3 => "Enterprise",
        _ => "Unknown"
    };
}
