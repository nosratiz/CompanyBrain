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
    /// Re-indexes all chunks of a document, skipping the embedding call if the content hash
    /// matches what is already stored. Returns <c>false</c> when no provider is configured.
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

        // Split content into chunks; fall back to full content if no chunks meet min length.
        var chunks = SearchUtilities.ExtractSnippets(fullContent).ToList();
        if (chunks.Count == 0)
        {
            chunks.Add(fullContent.Length > 1200 ? fullContent[..1200] : fullContent);
        }

        // Batch-embed all chunks in one API call.
        var generated = await generator
            .GenerateAsync(chunks, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Delete all existing chunks for this URI before upserting fresh ones.
        await store.DeleteByUriAsync(resourceUri, cancellationToken).ConfigureAwait(false);

        for (var i = 0; i < chunks.Count; i++)
        {
            var vector = generated[i].Vector.ToArray();
            await store.UpsertAsync(
                resourceUri,
                chunkIndex: i,
                collectionId,
                redactedSnippet: chunks[i],
                hash,
                factory.ResolvedModel,
                vector,
                cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Indexed {Uri} ({Chunks} chunks, {Dim} dims, model {Model}).",
            resourceUri, chunks.Count, generated[0].Vector.Length, factory.ResolvedModel);
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
        return store is null ? Task.CompletedTask : store.DeleteByUriAsync(resourceUri, cancellationToken);
    }

    internal static string ComputeHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(hash);
    }

}

