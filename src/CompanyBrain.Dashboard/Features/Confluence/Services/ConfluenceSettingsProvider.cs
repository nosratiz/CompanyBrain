using System.Security.Cryptography;
using System.Text;
using CompanyBrain.Dashboard.Features.Confluence.Data;
using CompanyBrain.Dashboard.Features.Confluence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CompanyBrain.Dashboard.Features.Confluence.Services;

/// <summary>
/// Provides resolved Confluence settings, preferring database-stored credentials over appsettings.json.
/// Also handles encrypted API token storage using AES-GCM.
/// </summary>
public sealed class ConfluenceSettingsProvider(
    IDbContextFactory<ConfluenceDbContext> dbContextFactory,
    IOptions<ConfluenceSyncOptions> options,
    ILogger<ConfluenceSettingsProvider> logger)
{
    private static readonly string KeyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CompanyBrain", ".confluence_key");

    private readonly byte[] _encryptionKey = GetOrCreateEncryptionKey();

    public async Task<ConfluenceSyncOptions> GetEffectiveOptionsAsync(CancellationToken cancellationToken = default)
    {
        var baseOptions = options.Value;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await db.Credentials
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null)
            return baseOptions;

        try
        {
            var decryptedToken = Decrypt(stored.EncryptedApiToken, stored.EncryptionNonce, stored.AuthTag);
            return new ConfluenceSyncOptions
            {
                Domain = stored.Domain,
                Email = stored.Email,
                ApiToken = decryptedToken,
                LocalBasePath = baseOptions.LocalBasePath,
                SyncIntervalMinutes = baseOptions.SyncIntervalMinutes
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt stored Confluence credentials, falling back to appsettings");
            return baseOptions;
        }
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var opts = await GetEffectiveOptionsAsync(cancellationToken);
        return !string.IsNullOrEmpty(opts.Domain)
            && !string.IsNullOrEmpty(opts.Email)
            && !string.IsNullOrEmpty(opts.ApiToken);
    }

    public async Task SaveCredentialsAsync(
        string domain,
        string email,
        string apiToken,
        CancellationToken cancellationToken = default)
    {
        var (encrypted, nonce, tag) = Encrypt(apiToken);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.Credentials
            .Where(c => c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), cancellationToken);

        db.Credentials.Add(new ConfluenceCredentials
        {
            Domain = domain.Trim().ToLowerInvariant(),
            Email = email.Trim(),
            EncryptedApiToken = encrypted,
            EncryptionNonce = nonce,
            AuthTag = tag
        });

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved Confluence credentials for {Domain} / {Email}", domain, email);
    }

    public async Task ClearCredentialsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Credentials
            .Where(c => c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), cancellationToken);
    }

    private (byte[] Data, byte[] Nonce, byte[] Tag) Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipher = new byte[plainBytes.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return (cipher, nonce, tag);
    }

    private string Decrypt(byte[] data, byte[] nonce, byte[] tag)
    {
        var plain = new byte[data.Length];
        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, data, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] GetOrCreateEncryptionKey()
    {
        var dir = Path.GetDirectoryName(KeyPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(KeyPath))
            return File.ReadAllBytes(KeyPath);

        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        File.WriteAllBytes(KeyPath, key);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(KeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return key;
    }
}
