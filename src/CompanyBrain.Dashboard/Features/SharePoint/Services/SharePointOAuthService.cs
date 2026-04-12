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
        CancellationToken cancellationToken = default)
    {
        var options = await settingsProvider.GetEffectiveOptionsAsync(cancellationToken);
        
        // Check if we need to recreate the client (settings changed)
        if (_confidentialClient is not null && OptionsMatch(options))
            return _confidentialClient;

        if (string.IsNullOrEmpty(options.ClientSecret))
        {
            throw new InvalidOperationException(
                "Client secret is required for confidential client application. " +
                "Configure it in the Settings page or appsettings.json.");
        }

        _confidentialClient = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithClientSecret(options.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{options.TenantId}")
            .Build();

        _lastOptions = options;
        return _confidentialClient;
    }

    private bool OptionsMatch(SharePointSyncOptions options)
    {
        return _lastOptions is not null &&
               _lastOptions.ClientId == options.ClientId &&
               _lastOptions.TenantId == options.TenantId &&
               _lastOptions.ClientSecret == options.ClientSecret;
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
    /// Acquires token for daemon/service scenarios using client credentials.
    /// </summary>
    public async Task<AuthenticationResult> AcquireTokenForClientAsync(
        CancellationToken cancellationToken = default)
    {
        var cca = await GetConfidentialClientApplicationAsync(cancellationToken);
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
