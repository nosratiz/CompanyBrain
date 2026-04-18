using Azure.Identity;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace CompanyBrain.Dashboard.Features.SharePoint.Services;

/// <summary>
/// Factory for creating authenticated Microsoft Graph clients.
/// Uses MSAL tokens from SharePointOAuthService for authentication.
/// </summary>
public sealed class GraphClientFactory(
    SharePointOAuthService oAuthService,
    SharePointSettingsProvider settingsProvider,
    ILogger<GraphClientFactory> logger)
{
    private GraphServiceClient? _clientCredentialsClient;
    private SharePointSyncOptions? _lastOptions;

    /// <summary>
    /// Creates a Graph client for the specified tenant using a stored delegated access token.
    /// The token is stored encrypted in SQLite during the "Connect to SharePoint" OAuth flow.
    /// Returns null if no valid (non-expired) token is available — user must reconnect.
    /// </summary>
    public async Task<GraphServiceClient?> CreateDelegatedClientAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await oAuthService.GetValidAccessTokenAsync(tenantId, cancellationToken);

        if (accessToken is null)
        {
            logger.LogWarning("No valid stored access token for tenant {TenantId} — user must reconnect SharePoint", tenantId);
            return null;
        }

        var tokenProvider = new StaticTokenProvider(accessToken);
        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
        return new GraphServiceClient(authProvider);
    }

    /// <summary>
    /// Creates a Graph client using client credentials (app-only permissions).
    /// Suitable for background sync operations.
    /// </summary>
    public async Task<GraphServiceClient> CreateClientCredentialsClientAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        
        // Check if we need to recreate the client (settings changed)
        if (_clientCredentialsClient is not null && 
            _lastOptions is not null &&
            _lastOptions.ClientId == options.ClientId &&
            _lastOptions.TenantId == options.TenantId &&
            _lastOptions.ClientSecret == options.ClientSecret)
        {
            return _clientCredentialsClient;
        }

        if (string.IsNullOrEmpty(options.ClientSecret))
        {
            throw new InvalidOperationException(
                "Client secret is required for client credentials authentication. " +
                "Configure SharePoint settings in the Settings page or appsettings.json.");
        }

        var credential = new ClientSecretCredential(
            options.TenantId,
            options.ClientId,
            options.ClientSecret);

        _clientCredentialsClient = new GraphServiceClient(credential,
            ["https://graph.microsoft.com/.default"]);
        _lastOptions = options;

        logger.LogDebug("Created new Graph client with credentials from {Source}", 
            string.IsNullOrWhiteSpace(_lastOptions.ClientId) ? "appsettings.json" : "UI settings");

        return _clientCredentialsClient;
    }

    /// <summary>
    /// Creates a Graph client for interactive scenarios (e.g., user sign-in).
    /// </summary>
    public async Task<GraphServiceClient> CreateInteractiveClientAsync(
        CancellationToken cancellationToken = default)
    {
        var authResult = await oAuthService.AcquireTokenInteractiveAsync(cancellationToken);
        var tokenProvider = new StaticTokenProvider(authResult.AccessToken);
        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
        return new GraphServiceClient(authProvider);
    }

    /// <summary>
    /// Creates a Graph client using a pre-acquired access token.
    /// Used with Microsoft.Identity.Web's ITokenAcquisition for the current OIDC user.
    /// </summary>
    public GraphServiceClient CreateFromAccessToken(string accessToken)
    {
        var tokenProvider = new StaticTokenProvider(accessToken);
        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
        return new GraphServiceClient(authProvider);
    }

    /// <summary>
    /// Gets the best available client for background operations.
    /// Prefers client credentials if configured, otherwise tries delegated.
    /// </summary>
    public async Task<GraphServiceClient?> GetBackgroundClientAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        
        // Try client credentials first if available
        if (!string.IsNullOrEmpty(options.ClientSecret))
        {
            try
            {
                return await CreateClientCredentialsClientAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Client credentials auth failed, trying delegated");
            }
        }

        // Fall back to delegated auth
        return await CreateDelegatedClientAsync(tenantId, cancellationToken);
    }

    /// <summary>
    /// Simple token provider for static access tokens.
    /// </summary>
    private sealed class StaticTokenProvider(string accessToken) : IAccessTokenProvider
    {
        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(accessToken);
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();
    }
}
