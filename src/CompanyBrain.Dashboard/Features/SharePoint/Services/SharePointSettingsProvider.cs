using CompanyBrain.Dashboard.Features.SharePoint.Models;
using CompanyBrain.Dashboard.Services;
using Microsoft.Extensions.Options;

namespace CompanyBrain.Dashboard.Features.SharePoint.Services;

/// <summary>
/// Provides SharePoint configuration by merging UI/database settings with appsettings.json.
/// Database settings take precedence when configured.
/// </summary>
public sealed class SharePointSettingsProvider(
    SettingsService settingsService,
    IOptions<SharePointSyncOptions> fallbackOptions,
    ILogger<SharePointSettingsProvider> logger)
{
    private readonly SharePointSyncOptions _fallback = fallbackOptions.Value;

    /// <summary>
    /// Gets the effective SharePoint sync options, preferring database settings over appsettings.json.
    /// </summary>
    public async Task<SharePointSyncOptions> GetEffectiveOptionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var appSettings = await settingsService.GetSettingsAsync(cancellationToken);

            // If SharePoint sync is not enabled in UI, return fallback config
            if (!appSettings.SharePointSyncEnabled)
            {
                logger.LogDebug("SharePoint sync disabled in UI settings, using appsettings.json config");
                return _fallback;
            }

            // If UI settings are configured, use them
            if (!string.IsNullOrWhiteSpace(appSettings.SharePointClientId) &&
                !string.IsNullOrWhiteSpace(appSettings.SharePointTenantId))
            {
                logger.LogDebug("Using SharePoint settings from UI/database");
                return new SharePointSyncOptions
                {
                    ClientId = appSettings.SharePointClientId,
                    TenantId = appSettings.SharePointTenantId,
                    ClientSecret = appSettings.SharePointClientSecret,
                    SyncIntervalMinutes = appSettings.SharePointSyncIntervalMinutes > 0 
                        ? appSettings.SharePointSyncIntervalMinutes 
                        : _fallback.SyncIntervalMinutes,
                    LocalBasePath = !string.IsNullOrWhiteSpace(appSettings.SharePointLocalBasePath)
                        ? appSettings.SharePointLocalBasePath
                        : _fallback.LocalBasePath,
                    DownloadChunkSize = _fallback.DownloadChunkSize,
                    GraphScopes = _fallback.GraphScopes
                };
            }

            // Fall back to appsettings.json
            logger.LogDebug("No UI SharePoint settings configured, using appsettings.json");
            return _fallback;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load SharePoint settings from database, using fallback");
            return _fallback;
        }
    }

    /// <summary>
    /// Checks if SharePoint sync is enabled and properly configured.
    /// </summary>
    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var options = await GetEffectiveOptionsAsync(cancellationToken);
        
        return !string.IsNullOrWhiteSpace(options.ClientId) &&
               !string.IsNullOrWhiteSpace(options.TenantId) &&
               options.ClientId != "YOUR_AZURE_AD_CLIENT_ID" &&
               options.TenantId != "YOUR_AZURE_AD_TENANT_ID";
    }
}
