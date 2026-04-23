using System.Net;
using System.Net.Http.Headers;
using CompanyBrain.Dashboard.Features.Auth.Services;
using CompanyBrain.Dashboard.Middleware;

namespace CompanyBrain.Dashboard.Features.License;

/// <summary>
/// Calls the external Panel API to fetch the current user's license.
/// </summary>
internal sealed class LicenseApiClient(HttpClient httpClient, AuthTokenStore tokenStore, ILogger<LicenseApiClient> logger)
{
    public async Task<LicenseInfo?> GetCurrentLicenseAsync(CancellationToken cancellationToken = default)
    {
        if (!tokenStore.IsAuthenticated)
            return null;

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenStore.Token);

        // Let UnauthorizedApiException from the handler propagate — the caller
        // (LicenseStateService) handles it by clearing the token and redirecting.
        var response = await httpClient.GetAsync("/api/licenses/current", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogDebug("License check returned {StatusCode}", response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LicenseInfo>(cancellationToken);
    }
}

public sealed record LicenseInfo(
    Guid Id,
    Guid UserId,
    string PlanName,
    LicenseTier Tier,
    DateTime PurchasedAt,
    DateTime? ExpiresAt,
    int MaxApiKeys,
    int MaxDocuments,
    long MaxStorageBytes,
    bool IsActive);
