using CompanyBrain.Dashboard.Features.Auth.Services;
using CompanyBrain.Dashboard.Middleware;
using Microsoft.AspNetCore.Components;

namespace CompanyBrain.Dashboard.Features.License;

/// <summary>
/// Scoped service that holds the resolved license tier for the current Blazor circuit.
/// Loaded once during layout initialization, then consumed by nav menu and pages.
/// </summary>
internal sealed class LicenseStateService(
    LicenseApiClient licenseApiClient,
    AuthTokenStore tokenStore,
    NavigationManager navigation,
    ILogger<LicenseStateService> logger)
{
    private bool _loaded;

    public LicenseTier Tier { get; private set; } = LicenseTier.Free;
    public LicenseInfo? License { get; private set; }
    public bool IsLoaded => _loaded;

    /// <summary>
    /// Fetches the current user's license from the API. Safe to call multiple times — only hits the API once.
    /// On 401, clears the token and redirects to login.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
            return;

        try
        {
            License = await licenseApiClient.GetCurrentLicenseAsync(cancellationToken);
            Tier = License is { IsActive: true } ? License.Tier : LicenseTier.Free;
        }
        catch (UnauthorizedApiException)
        {
            logger.LogWarning("License API returned 401 — clearing session and redirecting to login");
            await tokenStore.ClearAsync();
            navigation.NavigateTo("/login", forceLoad: true);
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load license — defaulting to Free tier");
            Tier = LicenseTier.Free;
        }
        finally
        {
            _loaded = true;
        }
    }

    /// <summary>
    /// Force a refresh on next access (e.g. after upgrade).
    /// </summary>
    public void Invalidate()
    {
        _loaded = false;
        License = null;
        Tier = LicenseTier.Free;
    }

    // ── Convenience helpers for UI binding ──

    /// <summary>Tier 0+ (Free): Dashboard, Documents, Search, Import, MCP Server</summary>
    public bool CanAccessCore => true;

    /// <summary>Tier 1+ (Starter): SharePoint, Confluence, Settings</summary>
    public bool CanAccessIntegrations => Tier >= LicenseTier.Starter;

    /// <summary>Tier 2+ (Professional): Tool Builder, Zero-Touch Setup, and all future features</summary>
    public bool CanAccessAdvanced => Tier >= LicenseTier.Professional;
}
