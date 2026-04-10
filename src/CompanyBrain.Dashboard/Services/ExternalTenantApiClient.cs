using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// Client for fetching tenant information from the external tenant management API.
/// </summary>
public sealed class ExternalTenantApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalTenantApiClient> _logger;

    public ExternalTenantApiClient(HttpClient httpClient, ILogger<ExternalTenantApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches all tenants from the tenant API.
    /// </summary>
    public async Task<IReadOnlyList<TenantSummaryDto>> GetTenantsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<TenantListResponseDto>("/api/tenants", ct);
            return response?.Tenants ?? [];
        }
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
            var response = await _httpClient.GetFromJsonAsync<TenantSummaryDto>($"/api/tenants/{tenantId}", ct);
            return response;
        }
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
}

// Response DTOs matching the external CompanyBrainUserPanel API
public sealed record TenantListResponseDto(IReadOnlyList<TenantSummaryDto> Tenants);

public sealed record TenantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string Plan,
    DateTime CreatedAt);
