using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using CompanyBrain.Dashboard.Features.SharePoint.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;

namespace CompanyBrain.Dashboard.Features.AutoSetup.Services;

/// <summary>
/// Microsoft 365 authentication using Entra ID Device Code Flow.
/// No client secret required — the user approves via browser using a short code.
/// After login, stores tokens and auto-discovers the user's SharePoint root and frequent sites.
/// </summary>
public sealed class M365DeviceCodeAuthService(
    SharePointSettingsProvider settingsProvider,
    SharePointOAuthService oAuthService,
    CompanyBrain.Dashboard.Features.SharePoint.Services.GraphClientFactory graphClientFactory,
    IDbContextFactory<SharePointDbContext> dbContextFactory,
    ILogger<M365DeviceCodeAuthService> logger)
{
    /// <summary>
    /// Result of a device code authentication flow.
    /// </summary>
    public sealed record DeviceCodeResult(
        bool Success,
        string Message,
        string? UserCode = null,
        string? VerificationUrl = null,
        string? UserPrincipalName = null,
        IReadOnlyList<DiscoveredSite>? DiscoveredSites = null);

    /// <summary>
    /// A SharePoint site discovered during auto-registration.
    /// </summary>
    public sealed record DiscoveredSite(
        string SiteId,
        string DisplayName,
        string WebUrl,
        bool IsRootSite);

    /// <summary>
    /// Initiates the device code flow and returns the user code + verification URL.
    /// The caller should display these to the user, then call <see cref="CompleteDeviceCodeFlowAsync"/>
    /// or use the callback-based overload.
    /// </summary>
    public async Task<DeviceCodeResult> StartDeviceCodeFlowAsync(
        Func<DeviceCodeInfo, Task> deviceCodeCallback,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.TenantId))
        {
            return new DeviceCodeResult(false,
                "ClientId and TenantId must be configured. Set them in Settings before connecting.");
        }

        try
        {
            var pca = PublicClientApplicationBuilder
                .Create(options.ClientId)
                .WithAuthority("https://login.microsoftonline.com/common")
                .Build();

            DeviceCodeResult? codeResult = null;

            // Device code flow with callback
            var authResult = await pca.AcquireTokenWithDeviceCode(options.GraphScopes, async deviceCodeInfo =>
            {
                codeResult = new DeviceCodeResult(
                    true,
                    "Enter the code on the verification page.",
                    UserCode: deviceCodeInfo.UserCode,
                    VerificationUrl: deviceCodeInfo.VerificationUrl);

                await deviceCodeCallback(new DeviceCodeInfo(
                    deviceCodeInfo.UserCode,
                    deviceCodeInfo.VerificationUrl,
                    deviceCodeInfo.Message));

            }).ExecuteAsync(cancellationToken);

            // Store the token
            await StoreTokenAsync(options.TenantId, authResult, cancellationToken);

            // Auto-discover sites
            var sites = await DiscoverSitesAsync(authResult.AccessToken, cancellationToken);

            logger.LogInformation("Device code flow completed for {User}, discovered {Count} sites",
                authResult.Account.Username, sites.Count);

            return new DeviceCodeResult(
                true,
                $"Connected as {authResult.Account.Username}",
                UserPrincipalName: authResult.Account.Username,
                DiscoveredSites: sites);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "authorization_pending")
        {
            return new DeviceCodeResult(false, "Authorization timed out — the user did not approve in time.");
        }
        catch (OperationCanceledException)
        {
            return new DeviceCodeResult(false, "Device code flow was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Device code flow failed");
            return new DeviceCodeResult(false, $"Authentication failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Simplified device code flow that acquires token and returns the result.
    /// The deviceCodeCallback is invoked with user code + URL for display.
    /// </summary>
    public async Task<DeviceCodeResult> AuthenticateWithDeviceCodeAsync(
        Action<string, string, string> onDeviceCode,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.TenantId))
        {
            return new DeviceCodeResult(false,
                "ClientId and TenantId must be configured first.");
        }

        try
        {
            var pca = PublicClientApplicationBuilder
                .Create(options.ClientId)
                .WithAuthority("https://login.microsoftonline.com/common")
                .Build();

            var authResult = await pca.AcquireTokenWithDeviceCode(options.GraphScopes, deviceCodeInfo =>
            {
                onDeviceCode(
                    deviceCodeInfo.UserCode,
                    deviceCodeInfo.VerificationUrl,
                    deviceCodeInfo.Message);
                return Task.CompletedTask;
            }).ExecuteAsync(cancellationToken);

            await StoreTokenAsync(options.TenantId, authResult, cancellationToken);

            var sites = await DiscoverSitesAsync(authResult.AccessToken, cancellationToken);

            return new DeviceCodeResult(
                true,
                $"Connected as {authResult.Account.Username}",
                UserPrincipalName: authResult.Account.Username,
                DiscoveredSites: sites);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "authorization_pending")
        {
            return new DeviceCodeResult(false, "Authorization timed out.");
        }
        catch (OperationCanceledException)
        {
            return new DeviceCodeResult(false, "Cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Device code auth failed");
            return new DeviceCodeResult(false, $"Failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Auto-discovers the user's root site and frequently accessed SharePoint sites.
    /// </summary>
    private async Task<IReadOnlyList<DiscoveredSite>> DiscoverSitesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var sites = new List<DiscoveredSite>();

        try
        {
            var client = graphClientFactory.CreateFromAccessToken(accessToken);

            // 1. Root site
            var rootSite = await client.Sites["root"]
                .GetAsync(r => r.QueryParameters.Select = ["id", "displayName", "webUrl"],
                    cancellationToken);

            if (rootSite is not null)
            {
                sites.Add(new DiscoveredSite(
                    rootSite.Id ?? "",
                    rootSite.DisplayName ?? "Root Site",
                    rootSite.WebUrl ?? "",
                    IsRootSite: true));
            }

            // 2. Followed sites (user's frequent sites)
            try
            {
                var followedSites = await client.Me.FollowedSites
                    .GetAsync(cancellationToken: cancellationToken);

                if (followedSites?.Value is not null)
                {
                    foreach (var site in followedSites.Value.Take(10))
                    {
                        sites.Add(new DiscoveredSite(
                            site.Id ?? "",
                            site.DisplayName ?? "Unknown",
                            site.WebUrl ?? "",
                            IsRootSite: false));
                    }
                }
            }
            catch (Exception ex)
            {
                // Followed sites may fail if the user hasn't followed any or lacks permissions
                logger.LogDebug(ex, "Could not retrieve followed sites — this is optional");
            }

            // 3. Search for recent sites via Graph search
            try
            {
                var searchResults = await client.Sites
                    .GetAsync(r =>
                    {
                        r.QueryParameters.Search = "*";
                        r.QueryParameters.Top = 10;
                        r.QueryParameters.Select = ["id", "displayName", "webUrl"];
                    }, cancellationToken);

                if (searchResults?.Value is not null)
                {
                    var existingIds = sites.Select(s => s.SiteId).ToHashSet();
                    foreach (var site in searchResults.Value)
                    {
                        if (site.Id is not null && !existingIds.Contains(site.Id))
                        {
                            sites.Add(new DiscoveredSite(
                                site.Id,
                                site.DisplayName ?? "Unknown",
                                site.WebUrl ?? "",
                                IsRootSite: false));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Site search failed — continuing with discovered sites");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Site discovery failed");
        }

        return sites;
    }

    private async Task StoreTokenAsync(
        string tenantId,
        AuthenticationResult authResult,
        CancellationToken cancellationToken)
    {
        var (encryptedData, nonce, tag) = oAuthService.EncryptToken(authResult.AccessToken);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Deactivate existing tokens
        await db.AuthTokens
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), cancellationToken);

        db.AuthTokens.Add(new SharePointAuthToken
        {
            TenantId = tenantId,
            UserPrincipalName = authResult.Account.Username,
            EncryptedRefreshToken = encryptedData,
            EncryptionNonce = nonce,
            AuthTag = tag,
            LastRefreshedAtUtc = authResult.ExpiresOn.UtcDateTime
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Info passed to the UI when the device code is ready.
    /// </summary>
    public sealed record DeviceCodeInfo(string UserCode, string VerificationUrl, string FullMessage);
}
