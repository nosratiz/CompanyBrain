namespace CompanyBrain.Search.Vector;

/// <summary>
/// Abstraction over the source of <see cref="EmbeddingOptions"/> so the runtime can be backed
/// by either appsettings (<c>IOptions&lt;EmbeddingOptions&gt;</c>) or a database-driven settings
/// service that allows operators to change provider/model/keys at runtime.
/// </summary>
public interface IEmbeddingOptionsAccessor
{
    /// <summary>
    /// Returns the current embedding options snapshot. Implementations should return a fresh
    /// instance on each call; consumers cache by <see cref="EmbeddingOptionsSignature"/>.
    /// </summary>
    EmbeddingOptions GetCurrent();
}

/// <summary>
/// Compact, value-equatable signature used to detect when cached generators/stores are stale.
/// </summary>
public readonly record struct EmbeddingOptionsSignature(
    EmbeddingProviderType Provider,
    string Model,
    int Dimensions,
    string ApiKeyHash,
    string Endpoint,
    string DatabasePath)
{
    public static EmbeddingOptionsSignature From(EmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var keyHash = string.IsNullOrEmpty(options.ApiKey)
            ? string.Empty
            : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(options.ApiKey)));

        return new EmbeddingOptionsSignature(
            options.Provider,
            options.Model ?? string.Empty,
            options.Dimensions,
            keyHash,
            options.Endpoint ?? string.Empty,
            options.DatabasePath ?? string.Empty);
    }
}
