using CompanyBrain.Dashboard.Data;
using CompanyBrain.Dashboard.Data.Models;
using CompanyBrain.Search.Vector;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CompanyBrain.Dashboard.Services;

/// <summary>
/// CRUD service for the singleton <see cref="DeepRootEmbeddingSettings"/> row. Encrypts the API
/// key with ASP.NET Data Protection on write and decrypts on read so the plaintext key never
/// touches disk.
/// </summary>
/// <remarks>
/// Does NOT depend on <see cref="EmbeddingProviderFactory"/> to avoid a circular DI chain
/// (Factory → IEmbeddingOptionsAccessor → DatabaseEmbeddingOptionsAccessor → this service).
/// Callers that need to signal the factory after a save should call
/// <c>EmbeddingProviderFactory.Reload()</c> themselves.
/// </remarks>
public sealed class DeepRootSettingsService
{
    private const string ProtectorPurpose = "CompanyBrain.DeepRoot.EmbeddingApiKey.v1";

    private readonly IDbContextFactory<DocumentAssignmentDbContext> contextFactory;
    private readonly IDataProtector protector;
    private readonly ILogger<DeepRootSettingsService> logger;

    public DeepRootSettingsService(
        IDbContextFactory<DocumentAssignmentDbContext> contextFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DeepRootSettingsService> logger)
    {
        this.contextFactory = contextFactory;
        this.protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        this.logger = logger;
    }

    /// <summary>
    /// Returns the saved settings as an <see cref="EmbeddingOptions"/> with the API key decrypted.
    /// Used both by the runtime accessor and by the Settings UI (which masks the key in the response).
    /// </summary>
    public async Task<EmbeddingOptions> GetEmbeddingOptionsAsync(CancellationToken cancellationToken = default)
    {
        var row = await LoadOrCreateAsync(cancellationToken).ConfigureAwait(false);
        return ToEmbeddingOptions(row);
    }

    /// <summary>
    /// Returns the raw row for UI display. The encrypted key is never decrypted here — the UI
    /// only needs to know whether one is set.
    /// </summary>
    public async Task<DeepRootEmbeddingSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await LoadOrCreateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the singleton row. When <paramref name="apiKey"/> is <c>null</c> the existing
    /// encrypted key is preserved; an empty string clears it.
    /// </summary>
    public async Task<DeepRootEmbeddingSettings> UpdateAsync(
        string provider,
        string model,
        int dimensions,
        string? apiKey,
        string endpoint,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var row = await context.DeepRootEmbeddingSettings
            .FirstOrDefaultAsync(s => s.Id == DeepRootEmbeddingSettingsConstants.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new DeepRootEmbeddingSettings { Id = DeepRootEmbeddingSettingsConstants.SingletonId };
            context.DeepRootEmbeddingSettings.Add(row);
        }

        row.Provider = provider;
        row.Model = model ?? string.Empty;
        row.Dimensions = dimensions < 0 ? 0 : dimensions;
        row.Endpoint = endpoint ?? string.Empty;
        row.DatabasePath = databasePath ?? string.Empty;
        row.UpdatedAtUtc = DateTime.UtcNow;

        if (apiKey is not null)
        {
            row.EncryptedApiKey = string.IsNullOrEmpty(apiKey)
                ? string.Empty
                : protector.Protect(apiKey);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("DeepRoot embedding settings updated: provider={Provider}, model={Model}, dim={Dim}",
            row.Provider, row.Model, row.Dimensions);
        return row;
    }

    private async Task<DeepRootEmbeddingSettings> LoadOrCreateAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var row = await context.DeepRootEmbeddingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == DeepRootEmbeddingSettingsConstants.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (row is not null) return row;

        row = new DeepRootEmbeddingSettings { Id = DeepRootEmbeddingSettingsConstants.SingletonId };
        context.DeepRootEmbeddingSettings.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row;
    }

    private EmbeddingOptions ToEmbeddingOptions(DeepRootEmbeddingSettings row)
    {
        var providerType = ParseProvider(row.Provider);
        var apiKey = string.IsNullOrEmpty(row.EncryptedApiKey)
            ? null
            : TryUnprotect(row.EncryptedApiKey);

        return new EmbeddingOptions
        {
            Provider = providerType,
            Model = row.Model ?? string.Empty,
            Dimensions = row.Dimensions,
            ApiKey = apiKey,
            Endpoint = string.IsNullOrWhiteSpace(row.Endpoint) ? null : row.Endpoint,
            DatabasePath = string.IsNullOrWhiteSpace(row.DatabasePath) ? null : row.DatabasePath,
        };
    }

    private string? TryUnprotect(string ciphertext)
    {
        try
        {
            return protector.Unprotect(ciphertext);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decrypt stored embedding API key. Returning null.");
            return null;
        }
    }

    private static EmbeddingProviderType ParseProvider(string raw)
    {
        return Enum.TryParse<EmbeddingProviderType>(raw, ignoreCase: true, out var p)
            ? p
            : EmbeddingProviderType.None;
    }
}
