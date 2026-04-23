using System.Security.Cryptography;
using System.Text;
using CompanyBrain.Utilities;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Search.Vector;

/// <summary>
/// Computes content hashes and only re-embeds documents when the hash has changed.
/// Resolves the active <see cref="EmbeddingProviderFactory"/> on every call so runtime settings
/// changes (provider/model/key) are picked up without restarting the indexer.
/// </summary>
public sealed class DocumentEmbeddingIndexer
{
    private readonly EmbeddingProviderFactory factory;
    private readonly ILogger<DocumentEmbeddingIndexer> logger;

    public DocumentEmbeddingIndexer(
        EmbeddingProviderFactory factory,
        ILogger<DocumentEmbeddingIndexer> logger)
    {
        this.factory = factory;
        this.logger = logger;
    }

    /// <summary>
    /// True when an embedding provider is configured. When false, callers should fall back
    /// to keyword search and skip the index write path.
    /// </summary>
    public bool IsEnabled => factory.GetGeneratorOrNull() is not null && factory.GetStoreOrNull() is not null;

    /// <summary>
    /// Re-indexes a single document, skipping the embedding call if the content hash matches
    /// what's already stored. Returns <c>false</c> when no provider is configured.
    /// </summary>
    public async Task<bool> IndexAsync(
        string resourceUri,
        string collectionId,
        string fullContent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceUri);

        var generator = factory.GetGeneratorOrNull();
        var store = factory.GetStoreOrNull();
        if (generator is null || store is null)
        {
            return false;
        }

        var hash = ComputeHash(fullContent);
        var existing = await store.GetStoredHashAsync(resourceUri, cancellationToken).ConfigureAwait(false);
        if (string.Equals(existing, hash, StringComparison.Ordinal))
        {
            logger.LogDebug("Delta-skip: {Uri} unchanged (hash {Hash}).", resourceUri, hash[..8]);
            return false;
        }

        var snippet = BuildRedactedSnippet(fullContent);

        var generated = await generator
            .GenerateAsync([snippet], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var vector = generated[0].Vector.ToArray();

        await store.UpsertAsync(
            resourceUri,
            collectionId,
            snippet,
            hash,
            factory.ResolvedModel,
            vector,
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Indexed {Uri} ({Dim} dims, model {Model}).", resourceUri, vector.Length, factory.ResolvedModel);
        return true;
    }

    /// <summary>
    /// Embeds a query string and returns the top-K nearest snippets. Returns an empty list
    /// when no provider is configured.
    /// </summary>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK,
        string? collectionId,
        CancellationToken cancellationToken)
    {
        var generator = factory.GetGeneratorOrNull();
        var store = factory.GetStoreOrNull();
        if (generator is null || store is null)
        {
            return [];
        }

        var generated = await generator
            .GenerateAsync([query], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return await store.SearchTopKAsync(
            generated[0].Vector.ToArray(),
            topK,
            collectionId,
            cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string resourceUri, CancellationToken cancellationToken)
    {
        var store = factory.GetStoreOrNull();
        return store is null ? Task.CompletedTask : store.DeleteAsync(resourceUri, cancellationToken);
    }

    internal static string ComputeHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// First non-empty paragraph, capped at ~1.2 KB. Real PII redaction lives in a follow-up.
    /// </summary>
    internal static string BuildRedactedSnippet(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var firstSnippet = SearchUtilities.ExtractSnippets(content).FirstOrDefault() ?? content;
        var trimmed = firstSnippet.Trim();
        return trimmed.Length > 1200 ? trimmed[..1200] : trimmed;
    }
}
