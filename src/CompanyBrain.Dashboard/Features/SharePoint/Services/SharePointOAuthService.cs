using System.Security.Cryptography;
using System.Text;
using CompanyBrain.Dashboard.Features.SharePoint.Data;
using CompanyBrain.Dashboard.Features.SharePoint.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;

namespace CompanyBrain.Dashboard.Features.SharePoint.Services;

/// <summary>
/// OAuth service using MSAL.NET for acquiring and managing Microsoft Graph tokens.
/// Stores encrypted refresh tokens in SQLite for background sync operations.
/// </summary>
public sealed class SharePointOAuthService(
    IDbContextFactory<SharePointDbContext> dbContextFactory,
    SharePointSettingsProvider settingsProvider,
    ILogger<SharePointOAuthService> logger)
{
    private static readonly string TokenCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CompanyBrain",
        ".msal_token_cache");

    private readonly byte[] _encryptionKey = GetOrCreateEncryptionKey();
    private IPublicClientApplication? _publicClient;
    private IConfidentialClientApplication? _confidentialClient;
    private SharePointSyncOptions? _lastOptions;

    /// <summary>
    /// Gets the public client application for interactive auth flows.
    /// </summary>
    public async Task<IPublicClientApplication> GetPublicClientApplicationAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        
        // Check if we need to recreate the client (settings changed)
        if (_publicClient is not null && OptionsMatch(options))
            return _publicClient;

        _publicClient = PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{options.TenantId}")
            .WithRedirectUri("http://localhost")
            .Build();

        _lastOptions = options;
        return _publicClient;
    }

    /// <summary>
    /// Gets the confidential client application for daemon/service scenarios.
    /// </summary>
    public async Task<IConfidentialClientApplication> GetConfidentialClientApplicationAsync(
        string? redirectUri = null,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        
        // Recreate if settings changed or redirect URI differs
        if (_confidentialClient is not null && OptionsMatch(options) && redirectUri is null)
            return _confidentialClient;

        if (string.IsNullOrEmpty(options.ClientSecret))
        {
            throw new InvalidOperationException(
                "Client secret is required for confidential client application. " +
                "Configure it in the Settings page or appsettings.json.");
        }

        var builder = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithClientSecret(options.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{options.TenantId}");

        if (!string.IsNullOrEmpty(redirectUri))
            builder = builder.WithRedirectUri(redirectUri);

        var cca = builder.Build();

        RegisterTokenCacheCallbacks(cca);

        if (redirectUri is null)
        {
            _confidentialClient = cca;
            _lastOptions = options;
        }

        return cca;
    }

    private bool OptionsMatch(SharePointSyncOptions options)
    {
        return _lastOptions is not null &&
               _lastOptions.ClientId == options.ClientId &&
               _lastOptions.TenantId == options.TenantId &&
               _lastOptions.ClientSecret == options.ClientSecret;
    }

    /// <summary>
    /// Builds the Microsoft authorization URL for the OAuth2 auth code flow.
    /// The user will be redirected here to grant SharePoint access.
    /// </summary>
    public async Task<Uri> GetAuthorizationUrlAsync(
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        var cca = await GetConfidentialClientApplicationAsync(redirectUri: null, cancellationToken);

        return await cca.GetAuthorizationRequestUrl(options.GraphScopes)
            .WithRedirectUri(redirectUri)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Exchanges an authorization code for access + refresh tokens and stores them.
    /// </summary>
    public async Task<AuthenticationResult> AcquireTokenByAuthCodeAsync(
        string authCode,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        var cca = await GetConfidentialClientApplicationAsync(redirectUri, cancellationToken);

        var result = await cca.AcquireTokenByAuthorizationCode(options.GraphScopes, authCode)
            .ExecuteAsync(cancellationToken);

        // Store the access token encrypted so sync can use it directly without MSAL cache
        if (result.Account is not null)
        {
            await StoreAccessTokenAsync(
                result.TenantId,
                result.Account.Username,
                result.AccessToken,
                result.ExpiresOn,
                cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Checks whether a SharePoint connection is active for the given tenant.
    /// </summary>
    public async Task<SharePointAuthToken?> GetActiveConnectionAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        return await GetStoredTokenAsync(tenantId, cancellationToken);
    }

    /// <summary>
    /// Disconnects SharePoint by deactivating all stored tokens for the tenant.
    /// </summary>
    public async Task DisconnectAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.AuthTokens
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), cancellationToken);

        logger.LogInformation("Disconnected SharePoint for tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Acquires a token interactively for the specified user.
    /// </summary>
    public async Task<AuthenticationResult> AcquireTokenInteractiveAsync(
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        var pca = await GetPublicClientApplicationAsync(cancellationToken);
        var result = await pca.AcquireTokenInteractive(options.GraphScopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);

        // Store the refresh token for background sync
        if (!string.IsNullOrEmpty(result.Account?.HomeAccountId.Identifier))
        {
            await StoreRefreshTokenAsync(
                result.TenantId,
                result.Account.Username,
                // Note: In real MSAL, you'd use token cache serialization
                // This is a simplified approach storing the account for silent re-auth
                result.UniqueId,
                cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Acquires a token silently using the cached refresh token.
    /// </summary>
    public async Task<AuthenticationResult?> AcquireTokenSilentAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        var pca = await GetPublicClientApplicationAsync(cancellationToken);
        var accounts = await pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.HomeAccountId.TenantId == tenantId);

        if (account is null)
        {
            logger.LogWarning("No cached account found for tenant {TenantId}", tenantId);
            return null;
        }

        try
        {
            return await pca.AcquireTokenSilent(options.GraphScopes, account)
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException ex)
        {
            logger.LogWarning(ex, "Silent token acquisition failed, user interaction required");
            return null;
        }
    }

    /// <summary>
    /// Acquires a token silently using the confidential client's in-memory cache.
    /// Works for tokens originally obtained via the auth code flow (Connect to SharePoint).
    /// </summary>
    public async Task<AuthenticationResult?> AcquireTokenSilentWithConfidentialClientAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);

        // Load the stored account identifier written during the auth code exchange
        var storedToken = await GetStoredTokenAsync(tenantId, cancellationToken);
        if (storedToken is null)
        {
            logger.LogDebug("No stored token record for tenant {TenantId} — reconnect SharePoint", tenantId);
            return null;
        }

        string accountIdentifier;
        try
        {
            accountIdentifier = DecryptToken(
                storedToken.EncryptedRefreshToken,
                storedToken.EncryptionNonce,
                storedToken.AuthTag);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt stored account identifier for tenant {TenantId}", tenantId);
            return null;
        }

        try
        {
            var cca = await GetConfidentialClientApplicationAsync(cancellationToken: cancellationToken);
            var account = await cca.GetAccountAsync(accountIdentifier);

            if (account is null)
            {
                logger.LogDebug("Account {Identifier} not found in token cache for tenant {TenantId}", accountIdentifier, tenantId);
                return null;
            }

            return await cca.AcquireTokenSilent(options.GraphScopes, account)
                .ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException ex)
        {
            logger.LogDebug(ex, "Silent token acquisition requires interaction for tenant {TenantId}", tenantId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Silent token acquisition failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    /// <summary>
    /// Acquires token for daemon/service scenarios using client credentials.
    /// </summary>
    public async Task<AuthenticationResult> AcquireTokenForClientAsync(
        CancellationToken cancellationToken = default)
    {
        var cca = await GetConfidentialClientApplicationAsync(redirectUri: null, cancellationToken);
        var scopes = new[] { "https://graph.microsoft.com/.default" };

        return await cca.AcquireTokenForClient(scopes)
            .ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a stored auth token record from the database.
    /// </summary>
    public async Task<SharePointAuthToken?> GetStoredTokenAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AuthTokens
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Stores an encrypted access token in the database, replacing any existing active token for the tenant.
    /// Uses LastRefreshedAtUtc to track token expiry.
    /// </summary>
    private async Task StoreAccessTokenAsync(
        string tenantId,
        string userPrincipalName,
        string accessToken,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken)
    {
        var (encryptedData, nonce, tag) = EncryptToken(accessToken);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.AuthTokens
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), cancellationToken);

        var authToken = new SharePointAuthToken
        {
            TenantId = tenantId,
            UserPrincipalName = userPrincipalName,
            EncryptedRefreshToken = encryptedData,
            EncryptionNonce = nonce,
            AuthTag = tag,
            LastRefreshedAtUtc = expiresOn.UtcDateTime
        };

        db.AuthTokens.Add(authToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stored access token for {User} in tenant {TenantId}, expires {Expiry}",
            userPrincipalName, tenantId, expiresOn);
    }

    /// <summary>
    /// Retrieves a valid (non-expired) decrypted access token from the database, or null if none exists.
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var stored = await GetStoredTokenAsync(tenantId, cancellationToken);
        if (stored is null)
        {
            logger.LogDebug("No stored token for tenant {TenantId}", tenantId);
            return null;
        }

        if (stored.LastRefreshedAtUtc is not null && stored.LastRefreshedAtUtc <= DateTime.UtcNow)
        {
            logger.LogDebug("Stored access token for tenant {TenantId} has expired at {Expiry}", tenantId, stored.LastRefreshedAtUtc);
            return null;
        }

        try
        {
            var token = DecryptToken(stored.EncryptedRefreshToken, stored.EncryptionNonce, stored.AuthTag);

            // Reject anything that isn't a JWT (e.g. old MSAL account identifiers stored before this fix)
            if (token.Count(c => c == '.') < 2)
            {
                logger.LogWarning("Stored value for tenant {TenantId} is not a JWT — deactivating stale record, user must reconnect", tenantId);
                await DeactivateTokenAsync(stored.TenantId, stored.Id);
                return null;
            }

            return token;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt access token for tenant {TenantId}", tenantId);
            return null;
        }
    }

    private async Task DeactivateTokenAsync(string tenantId, int tokenId)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            await db.AuthTokens
                .Where(t => t.TenantId == tenantId && t.Id == tokenId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deactivate stale token {TokenId}", tokenId);
        }
    }

    /// <summary>
    /// Stores an encrypted refresh token in the database.
    /// </summary>
    private async Task StoreRefreshTokenAsync(
        string tenantId,
        string userPrincipalName,
        string tokenData,
        CancellationToken cancellationToken)
    {
        var (encryptedData, nonce, tag) = EncryptToken(tokenData);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Deactivate existing tokens for this tenant
        await db.AuthTokens
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), cancellationToken);

        // Store new token
        var authToken = new SharePointAuthToken
        {
            TenantId = tenantId,
            UserPrincipalName = userPrincipalName,
            EncryptedRefreshToken = encryptedData,
            EncryptionNonce = nonce,
            AuthTag = tag
        };

        db.AuthTokens.Add(authToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stored new auth token for {User} in tenant {TenantId}", 
            userPrincipalName, tenantId);
    }

    /// <summary>
    /// Encrypts a token using AES-GCM.
    /// </summary>
    private (byte[] EncryptedData, byte[] Nonce, byte[] Tag) EncryptToken(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipherText = new byte[plainBytes.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipherText, tag);

        return (cipherText, nonce, tag);
    }

    /// <summary>
    /// Decrypts a token using AES-GCM.
    /// </summary>
    public string DecryptToken(byte[] encryptedData, byte[] nonce, byte[] tag)
    {
        var plainText = new byte[encryptedData.Length];

        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, encryptedData, tag, plainText);

        return Encoding.UTF8.GetString(plainText);
    }

    /// <summary>
    /// Gets or creates a local encryption key for token storage.
    /// In production, consider using Azure Key Vault or DPAPI.
    /// </summary>
    private static void RegisterTokenCacheCallbacks(IConfidentialClientApplication cca)
    {
        var cacheDir = Path.GetDirectoryName(TokenCachePath)!;
        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);

        cca.UserTokenCache.SetBeforeAccess(args =>
        {
            if (File.Exists(TokenCachePath))
                args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(TokenCachePath));
        });

        cca.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
                File.WriteAllBytes(TokenCachePath, args.TokenCache.SerializeMsalV3());
        });
    }

    private static byte[] GetOrCreateEncryptionKey()
    {
        var keyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CompanyBrain",
            ".sharepoint_key");

        var directory = Path.GetDirectoryName(keyPath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }

        var key = new byte[32]; // 256-bit key
        RandomNumberGenerator.Fill(key);
        File.WriteAllBytes(keyPath, key);

        // Set restrictive permissions on Unix-like systems
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return key;
    }
}
